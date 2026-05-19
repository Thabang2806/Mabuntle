using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Results;
using Swyftly.Application.Identity;
using Swyftly.Application.Ledger;
using Swyftly.Domain.Ledger;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Payouts;

public static class PayoutEndpoints
{
    public static IEndpointRouteBuilder MapPayoutEndpoints(this IEndpointRouteBuilder app)
    {
        var sellerGroup = app.MapGroup("/api/seller")
            .WithTags("Seller Payouts")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly);

        sellerGroup.MapGet("/balance", GetSellerBalanceAsync)
            .WithName("GetSellerBalance")
            .WithSummary("Returns pending, available, and held balances for the authenticated seller.")
            .Produces<SellerBalanceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapGet("/payouts", GetSellerPayoutsAsync)
            .WithName("GetSellerPayouts")
            .WithSummary("Returns payout records for the authenticated seller.")
            .Produces<IReadOnlyCollection<SellerPayoutResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var adminGroup = app.MapGroup("/api/admin/payouts")
            .WithTags("Admin Payouts")
            .RequireAuthorization(SwyftlyPolicies.AdminOnly);

        adminGroup.MapGet("/pending", GetAdminPendingPayoutsAsync)
            .WithName("GetAdminPendingPayouts")
            .WithSummary("Returns pending and held seller payouts for admin review.")
            .Produces<IReadOnlyCollection<SellerPayoutResponse>>(StatusCodes.Status200OK);

        adminGroup.MapPost("/{id:guid}/hold", HoldPayoutAsync)
            .WithName("HoldSellerPayout")
            .WithSummary("Places a seller payout on hold and writes an audit log.")
            .Produces<SellerPayoutResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/{id:guid}/release", ReleasePayoutAsync)
            .WithName("ReleaseSellerPayout")
            .WithSummary("Releases a held seller payout back to pending and writes an audit log.")
            .Produces<SellerPayoutResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetSellerBalanceAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var balances = await dbContext.SellerBalances
            .AsNoTracking()
            .Where(balance => balance.SellerId == seller.Id)
            .OrderBy(balance => balance.Currency)
            .Select(balance => new SellerCurrencyBalanceResponse(
                balance.Currency,
                balance.PendingBalance,
                balance.AvailableBalance,
                balance.HeldBalance))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(new SellerBalanceResponse(seller.Id, balances));
    }

    private static async Task<IResult> GetSellerPayoutsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var payouts = await QueryPayouts(dbContext)
            .Where(payout => payout.SellerId == seller.Id)
            .OrderByDescending(payout => payout.CreatedAtUtc)
            .Select(payout => Map(payout))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(payouts);
    }

    private static async Task<IResult> GetAdminPendingPayoutsAsync(
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var payouts = await QueryPayouts(dbContext)
            .Where(payout => payout.Status == SellerPayoutStatus.Pending || payout.Status == SellerPayoutStatus.OnHold)
            .OrderBy(payout => payout.CreatedAtUtc)
            .Select(payout => Map(payout))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(payouts);
    }

    private static async Task<IResult> HoldPayoutAsync(
        Guid id,
        PayoutReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IPayoutAdministrationService payoutAdministrationService,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return HttpResults.Problem(
                title: "Payouts.ActorNotFound",
                detail: "The authenticated admin user id could not be read.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await payoutAdministrationService.HoldAsync(
            new PayoutHoldRequest(
                id,
                actorUserId.Value,
                GetActorRole(principal),
                request.Reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> ReleasePayoutAsync(
        Guid id,
        PayoutReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IPayoutAdministrationService payoutAdministrationService,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return HttpResults.Problem(
                title: "Payouts.ActorNotFound",
                detail: "The authenticated admin user id could not be read.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await payoutAdministrationService.ReleaseAsync(
            new PayoutReleaseRequest(
                id,
                actorUserId.Value,
                GetActorRole(principal),
                request.Reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static IQueryable<SellerPayout> QueryPayouts(SwyftlyDbContext dbContext) =>
        dbContext.SellerPayouts
            .Include(payout => payout.Items)
            .AsNoTracking();

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken)
            : null;
    }

    private static SellerPayoutResponse Map(SellerPayout payout) =>
        new(
            payout.Id,
            payout.SellerId,
            payout.Amount,
            payout.Currency,
            payout.Status.ToString(),
            payout.CreatedAtUtc,
            payout.HeldAtUtc,
            payout.HoldReason,
            payout.ReleasedAtUtc,
            payout.ReleaseReason,
            payout.Items
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new SellerPayoutItemResponse(
                    item.Id,
                    item.LedgerEntryId,
                    item.OrderId,
                    item.PaymentId,
                    item.Amount,
                    item.Currency,
                    item.CreatedAtUtc))
                .ToArray());

    private static string GetActorRole(ClaimsPrincipal principal) =>
        principal.IsInRole(SwyftlyRoles.SuperAdmin)
            ? SwyftlyRoles.SuperAdmin
            : SwyftlyRoles.Admin;

    private static Guid? GetActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "Payouts.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record PayoutReasonRequest(string Reason);

public sealed record SellerBalanceResponse(
    Guid SellerId,
    IReadOnlyCollection<SellerCurrencyBalanceResponse> Balances);

public sealed record SellerCurrencyBalanceResponse(
    string Currency,
    decimal PendingBalance,
    decimal AvailableBalance,
    decimal HeldBalance);

public sealed record SellerPayoutResponse(
    Guid PayoutId,
    Guid SellerId,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? HeldAtUtc,
    string? HoldReason,
    DateTimeOffset? ReleasedAtUtc,
    string? ReleaseReason,
    IReadOnlyCollection<SellerPayoutItemResponse> Items);

public sealed record SellerPayoutItemResponse(
    Guid PayoutItemId,
    Guid LedgerEntryId,
    Guid? OrderId,
    Guid? PaymentId,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAtUtc);
