using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Security;
using Swyftly.Application.Identity;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Authentication;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Registers a public buyer or seller account.")
            .RequireRateLimiting(SwyftlyRateLimitPolicies.Auth)
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Authenticates a user and returns JWT and refresh tokens.")
            .RequireRateLimiting(SwyftlyRateLimitPolicies.Auth)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshToken")
            .WithSummary("Rotates a valid refresh token and returns a new token pair.")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Revokes a refresh token.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", CurrentUserAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Returns the current authenticated user.")
            .RequireAuthorization()
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/policy-checks/admin", () => HttpResults.Ok(new PolicyCheckResponse(SwyftlyPolicies.AdminOnly)))
            .WithName("CheckAdminPolicy")
            .WithSummary("Verifies the admin-only authorization policy.")
            .RequireAuthorization(SwyftlyPolicies.AdminOnly)
            .Produces<PolicyCheckResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/policy-checks/seller", () => HttpResults.Ok(new PolicyCheckResponse(SwyftlyPolicies.SellerOnly)))
            .WithName("CheckSellerPolicy")
            .WithSummary("Verifies the seller-only authorization policy.")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly)
            .Produces<PolicyCheckResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var role = NormalizePublicRegistrationRole(request.Role);

        if (role is null)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Identity.InvalidRegistrationRole",
                "Public registration is limited to Buyer and Seller roles.");
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Problem(
                StatusCodes.Status409Conflict,
                "Identity.EmailAlreadyRegistered",
                "An account already exists for this email address.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            CreatedAtUtc = now
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return IdentityProblem(createResult, StatusCodes.Status400BadRequest, "Identity.RegistrationFailed");
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            return IdentityProblem(roleResult, StatusCodes.Status400BadRequest, "Identity.RoleAssignmentFailed");
        }

        SellerVerificationStatus? sellerVerificationStatus = null;
        if (string.Equals(role, SwyftlyRoles.Buyer, StringComparison.Ordinal))
        {
            dbContext.BuyerProfiles.Add(new BuyerProfile(user.Id));
        }
        else
        {
            var sellerProfile = new SellerProfile(user.Id);
            sellerVerificationStatus = sellerProfile.VerificationStatus;
            dbContext.SellerProfiles.Add(sellerProfile);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = new RegisterResponse(
            user.Id,
            email,
            role,
            sellerVerificationStatus?.ToString(),
            EmailVerificationRequired: false);

        return HttpResults.Created($"/api/auth/users/{user.Id}", response);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidCredentials",
                "The email address or password is incorrect.");
        }

        user.LastLoginAtUtc = timeProvider.GetUtcNow();
        await userManager.UpdateAsync(user);

        var response = await CreateAuthResponseAsync(
            user,
            userManager,
            jwtTokenService,
            dbContext,
            timeProvider,
            cancellationToken);

        return HttpResults.Ok(response);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var tokenHash = JwtTokenService.HashRefreshToken(request.RefreshToken);
        var refreshToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive(now))
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidRefreshToken",
                "The refresh token is invalid or expired.");
        }

        var tokenPair = await jwtTokenService.CreateTokenPairAsync(refreshToken.User, cancellationToken);
        var replacementHash = JwtTokenService.HashRefreshToken(tokenPair.RefreshToken);
        refreshToken.Revoke(now, replacementHash);
        dbContext.RefreshTokens.Add(new RefreshToken(
            refreshToken.UserId,
            replacementHash,
            tokenPair.RefreshTokenExpiresAtUtc,
            now));

        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(refreshToken.User);
        return HttpResults.Ok(ToAuthResponse(refreshToken.User, roles.ToArray(), tokenPair));
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tokenHash = JwtTokenService.HashRefreshToken(request.RefreshToken);
        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is not null && refreshToken.IsActive(timeProvider.GetUtcNow()))
        {
            refreshToken.Revoke(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return HttpResults.NoContent();
    }

    private static async Task<IResult> CurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidToken",
                "The access token does not identify a valid user.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.UserNotFound",
                "The authenticated user no longer exists.");
        }

        var roles = await userManager.GetRolesAsync(user);
        return HttpResults.Ok(new CurrentUserResponse(user.Id, user.Email ?? string.Empty, roles.ToArray()));
    }

    private static async Task<AuthResponse> CreateAuthResponseAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tokenPair = await jwtTokenService.CreateTokenPairAsync(user, cancellationToken);
        dbContext.RefreshTokens.Add(new RefreshToken(
            user.Id,
            JwtTokenService.HashRefreshToken(tokenPair.RefreshToken),
            tokenPair.RefreshTokenExpiresAtUtc,
            timeProvider.GetUtcNow()));

        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        return ToAuthResponse(user, roles.ToArray(), tokenPair);
    }

    private static AuthResponse ToAuthResponse(
        ApplicationUser user,
        IReadOnlyCollection<string> roles,
        TokenPair tokenPair)
    {
        return new AuthResponse(
            user.Id,
            user.Email ?? string.Empty,
            roles,
            tokenPair.AccessToken,
            tokenPair.AccessTokenExpiresAtUtc,
            tokenPair.RefreshToken,
            tokenPair.RefreshTokenExpiresAtUtc);
    }

    private static string? NormalizePublicRegistrationRole(string role)
    {
        return SwyftlyRoles.PublicRegistrationRoles
            .FirstOrDefault(publicRole => string.Equals(publicRole, role.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static IResult IdentityProblem(IdentityResult result, int statusCode, string title)
    {
        return HttpResults.ValidationProblem(
            result.Errors.ToDictionary(
                error => error.Code,
                error => new[] { error.Description }),
            title: title,
            statusCode: statusCode);
    }

    private static IResult Problem(int statusCode, string title, string detail)
    {
        return HttpResults.Problem(
            title: title,
            detail: detail,
            statusCode: statusCode);
    }
}

public sealed record RegisterRequest(
    string Email,
    string Password,
    string Role);

public sealed record RegisterResponse(
    Guid UserId,
    string Email,
    string Role,
    string? SellerVerificationStatus,
    bool EmailVerificationRequired);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RefreshTokenRequest(
    string RefreshToken);

public sealed record LogoutRequest(
    string RefreshToken);

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    IReadOnlyCollection<string> Roles,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    IReadOnlyCollection<string> Roles);

public sealed record PolicyCheckResponse(string Policy);
