using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Admin;
using Swyftly.Application.Identity;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Admin;

public static class AdminSellerEndpoints
{
    public static IEndpointRouteBuilder MapAdminSellerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/sellers")
            .WithTags("Admin Sellers")
            .RequireAuthorization(SwyftlyPolicies.AdminOnly);

        group.MapGet("/pending", GetPendingAsync)
            .WithName("GetPendingSellers")
            .WithSummary("Returns sellers submitted for verification review.")
            .Produces<IReadOnlyCollection<AdminSellerSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{sellerId:guid}", GetByIdAsync)
            .WithName("GetAdminSellerDetail")
            .WithSummary("Returns seller verification detail for admin review.")
            .Produces<AdminSellerDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{sellerId:guid}/approve", ApproveAsync)
            .WithName("ApproveSeller")
            .WithSummary("Approves a seller and marks the seller as verified.")
            .Produces<AdminSellerDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{sellerId:guid}/reject", RejectAsync)
            .WithName("RejectSeller")
            .WithSummary("Rejects a seller verification submission.")
            .Produces<AdminSellerDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{sellerId:guid}/suspend", SuspendAsync)
            .WithName("SuspendSeller")
            .WithSummary("Suspends a seller.")
            .Produces<AdminSellerDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetPendingAsync(
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sellers = await dbContext.SellerProfiles
            .Where(seller => seller.VerificationStatus == SellerVerificationStatus.UnderReview)
            .OrderBy(seller => seller.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        var responses = new List<AdminSellerSummaryResponse>();

        foreach (var seller in sellers)
        {
            var storefront = await dbContext.SellerStorefronts
                .SingleOrDefaultAsync(item => item.SellerId == seller.Id, cancellationToken);
            var latestVerification = await dbContext.SellerVerifications
                .Where(item => item.SellerId == seller.Id)
                .OrderByDescending(item => item.SubmittedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            responses.Add(new AdminSellerSummaryResponse(
                seller.Id,
                seller.DisplayName,
                seller.ContactEmail,
                storefront?.StoreName,
                storefront?.Slug,
                seller.VerificationStatus.ToString(),
                latestVerification?.SubmittedAtUtc));
        }

        return HttpResults.Ok(responses);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid sellerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var detail = await CreateDetailResponseAsync(sellerId, dbContext, cancellationToken);
        return detail is null ? SellerNotFound() : HttpResults.Ok(detail);
    }

    private static async Task<IResult> ApproveAsync(
        Guid sellerId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var related = await GetRelatedAsync(sellerId, dbContext, cancellationToken);
        if (related.PayoutProfile is null)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["payout"] = ["Seller payout placeholder must exist before approval."]
            });
        }

        var previousStatus = seller.VerificationStatus;
        related.PayoutProfile.MarkAdminApproved(GetActorUserId(principal) ?? Guid.Empty, timeProvider.GetUtcNow());

        try
        {
            seller.MarkVerified(related.Storefront, related.Address, related.PayoutProfile);
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["seller"] = [exception.Message]
            });
        }

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "SellerApproved",
            seller.Id,
            previousStatus,
            seller.VerificationStatus,
            reason: null,
            cancellationToken);

        await UpdateLatestVerificationAsync(seller.Id, dbContext, verification =>
        {
            verification.Approve(GetActorUserId(principal) ?? Guid.Empty, timeProvider.GetUtcNow());
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateDetailResponseAsync(sellerId, dbContext, cancellationToken));
    }

    private static async Task<IResult> RejectAsync(
        Guid sellerId,
        AdminSellerReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ReasonRequired();
        }

        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var previousStatus = seller.VerificationStatus;
        seller.MarkRejected(request.Reason);

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "SellerRejected",
            seller.Id,
            previousStatus,
            seller.VerificationStatus,
            request.Reason,
            cancellationToken);

        await UpdateLatestVerificationAsync(seller.Id, dbContext, verification =>
        {
            verification.Reject(GetActorUserId(principal) ?? Guid.Empty, timeProvider.GetUtcNow(), request.Reason);
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateDetailResponseAsync(sellerId, dbContext, cancellationToken));
    }

    private static async Task<IResult> SuspendAsync(
        Guid sellerId,
        AdminSellerReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ReasonRequired();
        }

        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var previousStatus = seller.VerificationStatus;
        seller.Suspend();

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "SellerSuspended",
            seller.Id,
            previousStatus,
            seller.VerificationStatus,
            request.Reason,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateDetailResponseAsync(sellerId, dbContext, cancellationToken));
    }

    private static async Task<AdminSellerDetailResponse?> CreateDetailResponseAsync(
        Guid sellerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return null;
        }

        var related = await GetRelatedAsync(sellerId, dbContext, cancellationToken);
        var auditTrail = await dbContext.AuditLogs
            .Where(auditLog => auditLog.EntityType == "SellerProfile" && auditLog.EntityId == sellerId.ToString())
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .Select(auditLog => new AdminAuditLogResponse(
                auditLog.Id,
                auditLog.ActionType,
                auditLog.ActorUserId,
                auditLog.ActorRole,
                auditLog.Reason,
                auditLog.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new AdminSellerDetailResponse(
            seller.Id,
            seller.UserId,
            seller.VerificationStatus.ToString(),
            seller.DisplayName,
            seller.ContactEmail,
            seller.PhoneNumber,
            seller.BusinessType?.ToString(),
            seller.BusinessName,
            related.Storefront is null
                ? null
                : new AdminSellerStorefrontResponse(
                    related.Storefront.StoreName,
                    related.Storefront.Slug,
                    related.Storefront.Description,
                    related.Storefront.LogoUrl,
                    related.Storefront.BannerUrl,
                    related.Storefront.IsPublished),
            related.Address is null
                ? null
                : new AdminSellerAddressResponse(
                    related.Address.AddressLine1,
                    related.Address.AddressLine2,
                    related.Address.City,
                    related.Address.Province,
                    related.Address.PostalCode,
                    related.Address.CountryCode),
            related.PayoutProfile is null
                ? null
                : new AdminSellerPayoutResponse(
                    related.PayoutProfile.PayoutProviderReference,
                    related.PayoutProfile.HasSubmittedPlaceholder,
                    related.PayoutProfile.IsAdminApproved),
            auditTrail);
    }

    private static async Task<SellerRelatedData> GetRelatedAsync(
        Guid sellerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var storefront = await dbContext.SellerStorefronts.SingleOrDefaultAsync(item => item.SellerId == sellerId, cancellationToken);
        var address = await dbContext.SellerAddresses.SingleOrDefaultAsync(item => item.SellerId == sellerId, cancellationToken);
        var payoutProfile = await dbContext.SellerPayoutProfiles.SingleOrDefaultAsync(item => item.SellerId == sellerId, cancellationToken);

        return new SellerRelatedData(storefront, address, payoutProfile);
    }

    private static async Task AddAuditLogAsync(
        IAuditLogService auditLogService,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        string actionType,
        Guid sellerId,
        SellerVerificationStatus previousStatus,
        SellerVerificationStatus newStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var actorUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var actorRole = principal.IsInRole(SwyftlyRoles.SuperAdmin)
            ? SwyftlyRoles.SuperAdmin
            : SwyftlyRoles.Admin;

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                actorUserId,
                actorRole,
                actionType,
                "SellerProfile",
                sellerId.ToString(),
                JsonSerializer.Serialize(new { verificationStatus = previousStatus.ToString() }),
                JsonSerializer.Serialize(new { verificationStatus = newStatus.ToString() }),
                reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
    }

    private static async Task UpdateLatestVerificationAsync(
        Guid sellerId,
        SwyftlyDbContext dbContext,
        Action<SellerVerification> update,
        CancellationToken cancellationToken)
    {
        var latestVerification = await dbContext.SellerVerifications
            .Where(verification => verification.SellerId == sellerId)
            .OrderByDescending(verification => verification.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVerification is not null)
        {
            update(latestVerification);
        }
    }

    private static Guid? GetActorUserId(ClaimsPrincipal principal)
    {
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;
    }

    private static IResult ReasonRequired() =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["reason"] = ["Reason is required."]
        });

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "AdminSellers.SellerNotFound",
            detail: "Seller was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private sealed record SellerRelatedData(
        SellerStorefront? Storefront,
        SellerAddress? Address,
        SellerPayoutProfilePlaceholder? PayoutProfile);
}

public sealed record AdminSellerSummaryResponse(
    Guid SellerId,
    string? DisplayName,
    string? ContactEmail,
    string? StoreName,
    string? StoreSlug,
    string VerificationStatus,
    DateTimeOffset? SubmittedAtUtc);

public sealed record AdminSellerDetailResponse(
    Guid SellerId,
    Guid UserId,
    string VerificationStatus,
    string? DisplayName,
    string? ContactEmail,
    string? PhoneNumber,
    string? BusinessType,
    string? BusinessName,
    AdminSellerStorefrontResponse? Storefront,
    AdminSellerAddressResponse? Address,
    AdminSellerPayoutResponse? Payout,
    IReadOnlyCollection<AdminAuditLogResponse> AuditTrail);

public sealed record AdminSellerStorefrontResponse(
    string StoreName,
    string Slug,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    bool IsPublished);

public sealed record AdminSellerAddressResponse(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Province,
    string PostalCode,
    string CountryCode);

public sealed record AdminSellerPayoutResponse(
    string PayoutProviderReference,
    bool HasSubmittedPlaceholder,
    bool IsAdminApproved);

public sealed record AdminAuditLogResponse(
    Guid Id,
    string ActionType,
    string? ActorUserId,
    string? ActorRole,
    string? Reason,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminSellerReasonRequest(string Reason);
