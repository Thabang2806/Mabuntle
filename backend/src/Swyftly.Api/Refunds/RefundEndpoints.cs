using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Results;
using Swyftly.Domain.Buyers;
using Swyftly.Application.Identity;
using Swyftly.Application.Refunds;
using Swyftly.Domain.Refunds;
using Swyftly.Infrastructure.Persistence;
using Swyftly.Infrastructure.Refunds;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Refunds;

public static class RefundEndpoints
{
    public static IEndpointRouteBuilder MapRefundEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/api/admin")
            .WithTags("Admin Refunds")
            .RequireAuthorization(SwyftlyPolicies.FinanceRead);

        var buyerGroup = app.MapGroup("/api/buyer")
            .WithTags("Buyer Refunds")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        buyerGroup.MapGet("/refunds", GetBuyerRefundsAsync)
            .WithName("GetBuyerRefunds")
            .WithSummary("Returns buyer-safe refund records for the authenticated buyer.")
            .Produces<IReadOnlyCollection<BuyerRefundResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapGet("/refunds/{refundId:guid}", GetBuyerRefundAsync)
            .WithName("GetBuyerRefund")
            .WithSummary("Returns one buyer-safe refund record for the authenticated buyer.")
            .Produces<BuyerRefundResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapGet("/orders/{orderId:guid}/refunds", GetBuyerOrderRefundsAsync)
            .WithName("GetBuyerOrderRefunds")
            .WithSummary("Returns buyer-safe refund records for one buyer-owned order.")
            .Produces<IReadOnlyCollection<BuyerRefundResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapGet("/returns/{returnRequestId:guid}/refunds", GetBuyerReturnRefundsAsync)
            .WithName("GetBuyerReturnRefunds")
            .WithSummary("Returns buyer-safe refund records for one buyer-owned return.")
            .Produces<IReadOnlyCollection<BuyerRefundResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/orders/{orderId:guid}/refunds", CreateOrderRefundAsync)
            .WithName("CreateOrderRefund")
            .WithSummary("Creates an admin refund request for an order.")
            .RequireAuthorization(SwyftlyPolicies.FinanceOperate)
            .Produces<RefundResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        adminGroup.MapPost("/returns/{returnRequestId:guid}/refunds", CreateReturnRefundAsync)
            .WithName("CreateReturnRefund")
            .WithSummary("Creates an admin refund request for a return.")
            .RequireAuthorization(SwyftlyPolicies.FinanceOperate)
            .Produces<RefundResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        adminGroup.MapGet("/refunds", GetRefundsAsync)
            .WithName("GetRefunds")
            .WithSummary("Returns refund records for admin review.")
            .Produces<IReadOnlyCollection<RefundResult>>(StatusCodes.Status200OK);

        adminGroup.MapPost("/refunds/{refundId:guid}/approve", ApproveRefundAsync)
            .WithName("ApproveRefund")
            .WithSummary("Approves and processes a refund through the payment provider abstraction.")
            .RequireAuthorization(SwyftlyPolicies.FinanceApprove)
            .Produces<RefundResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        adminGroup.MapPost("/refunds/{refundId:guid}/confirm-manual-provider-refund", ConfirmManualProviderRefundAsync)
            .WithName("ConfirmManualProviderRefund")
            .WithSummary("Confirms a manually completed provider refund and finalizes refund accounting.")
            .RequireAuthorization(SwyftlyPolicies.FinanceApprove)
            .Produces<RefundResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetBuyerRefundsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var refunds = await BuyerRefundQuery(dbContext)
            .Where(refund => refund.BuyerId == buyer.Id)
            .OrderByDescending(refund => refund.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(refunds.Select(MapBuyerRefund).ToArray());
    }

    private static async Task<IResult> GetBuyerRefundAsync(
        Guid refundId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var refund = await BuyerRefundQuery(dbContext)
            .SingleOrDefaultAsync(
                refund => refund.Id == refundId && refund.BuyerId == buyer.Id,
                cancellationToken);

        return refund is null
            ? RefundNotFound()
            : HttpResults.Ok(MapBuyerRefund(refund));
    }

    private static async Task<IResult> GetBuyerOrderRefundsAsync(
        Guid orderId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var orderExists = await dbContext.Orders
            .AsNoTracking()
            .AnyAsync(order => order.Id == orderId && order.BuyerId == buyer.Id, cancellationToken);
        if (!orderExists)
        {
            return OrderNotFound();
        }

        var refunds = await BuyerRefundQuery(dbContext)
            .Where(refund => refund.BuyerId == buyer.Id && refund.OrderId == orderId)
            .OrderByDescending(refund => refund.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(refunds.Select(MapBuyerRefund).ToArray());
    }

    private static async Task<IResult> GetBuyerReturnRefundsAsync(
        Guid returnRequestId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var returnExists = await dbContext.ReturnRequests
            .AsNoTracking()
            .AnyAsync(returnRequest => returnRequest.Id == returnRequestId && returnRequest.BuyerId == buyer.Id, cancellationToken);
        if (!returnExists)
        {
            return ReturnNotFound();
        }

        var refunds = await BuyerRefundQuery(dbContext)
            .Where(refund => refund.BuyerId == buyer.Id && refund.ReturnRequestId == returnRequestId)
            .OrderByDescending(refund => refund.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(refunds.Select(MapBuyerRefund).ToArray());
    }

    private static async Task<IResult> CreateOrderRefundAsync(
        Guid orderId,
        CreateRefundApiRequest request,
        ClaimsPrincipal principal,
        IRefundWorkflowService refundWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        var result = await refundWorkflowService.CreateRefundRequestAsync(
            new CreateRefundWorkflowRequest(
                orderId,
                null,
                request.Amount,
                request.Reason,
                actorUserId.Value,
                GetActorRole(principal),
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> CreateReturnRefundAsync(
        Guid returnRequestId,
        CreateRefundApiRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IRefundWorkflowService refundWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        var orderId = await dbContext.ReturnRequests
            .Where(returnRequest => returnRequest.Id == returnRequestId)
            .Select(returnRequest => returnRequest.OrderId)
            .SingleOrDefaultAsync(cancellationToken);
        if (orderId == Guid.Empty)
        {
            return HttpResults.Problem(
                title: "Refunds.ReturnNotFound",
                detail: "Return request was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var result = await refundWorkflowService.CreateRefundRequestAsync(
            new CreateRefundWorkflowRequest(
                orderId,
                returnRequestId,
                request.Amount,
                request.Reason,
                actorUserId.Value,
                GetActorRole(principal),
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> GetRefundsAsync(
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var refunds = await dbContext.Refunds
            .Include(refund => refund.Events)
            .OrderByDescending(refund => refund.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(refunds.Select(EfRefundWorkflowService.Map).ToArray());
    }

    private static IQueryable<Refund> BuyerRefundQuery(SwyftlyDbContext dbContext) =>
        dbContext.Refunds
            .Include(refund => refund.Events)
            .AsNoTracking();

    private static BuyerRefundResult MapBuyerRefund(Refund refund) =>
        new(
            refund.Id,
            refund.OrderId,
            refund.ReturnRequestId,
            refund.Amount,
            refund.Currency,
            refund.Status.ToString(),
            BuyerStatusMessage(refund),
            refund.RequestedAtUtc,
            refund.ApprovedAtUtc,
            refund.RefundedAtUtc,
            refund.Events
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new BuyerRefundTimelineEventResult(
                    item.Status.ToString(),
                    item.EventType,
                    BuyerTimelineMessage(item),
                    item.CreatedAtUtc))
                .ToArray());

    private static string BuyerStatusMessage(Refund refund) =>
        refund.Status switch
        {
            RefundStatus.Requested => "Your refund request has been recorded and is waiting for finance review.",
            RefundStatus.Approved => "Your refund has been approved and is waiting to be processed.",
            RefundStatus.Processing when refund.Events.Any(item => item.EventType == "ProviderRefundActionRequired") =>
                "Your refund needs finance or provider action before it can be completed.",
            RefundStatus.Processing => "Your refund is being processed.",
            RefundStatus.Refunded => "Your refund has been completed.",
            RefundStatus.Failed => "Your refund could not be completed. Contact support if you need help.",
            RefundStatus.Rejected => "This refund request was not approved.",
            _ => "Refund status updated."
        };

    private static string BuyerTimelineMessage(RefundEvent refundEvent) =>
        refundEvent.EventType switch
        {
            "RefundRequested" => "Refund request recorded.",
            "RefundApproved" => "Refund approved for processing.",
            "RefundProcessing" => "Refund processing started.",
            "ProviderRefundActionRequired" => "Finance or provider action is still in progress.",
            "Refunded" => "Refund completed.",
            "RefundFailed" => "Refund could not be completed.",
            _ => "Refund status updated."
        };

    private static async Task<IResult> ApproveRefundAsync(
        Guid refundId,
        ApproveRefundApiRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IRefundWorkflowService refundWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        var result = await refundWorkflowService.ApproveRefundAsync(
            new ApproveRefundWorkflowRequest(
                refundId,
                actorUserId.Value,
                GetActorRole(principal),
                request.Reason,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> ConfirmManualProviderRefundAsync(
        Guid refundId,
        ConfirmManualProviderRefundApiRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IRefundWorkflowService refundWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        var result = await refundWorkflowService.ConfirmManualProviderRefundAsync(
            new ConfirmManualProviderRefundWorkflowRequest(
                refundId,
                actorUserId.Value,
                GetActorRole(principal),
                request.ProviderRefundReference,
                request.Reason,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static string GetActorRole(ClaimsPrincipal principal) =>
        principal.IsInRole(SwyftlyRoles.SuperAdmin)
            ? SwyftlyRoles.SuperAdmin
            : principal.IsInRole(SwyftlyRoles.FinanceApprover)
                ? SwyftlyRoles.FinanceApprover
                : principal.IsInRole(SwyftlyRoles.FinanceOperator)
                    ? SwyftlyRoles.FinanceOperator
                    : SwyftlyRoles.Admin;

    private static Guid? GetActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }

    private static IResult BuyerNotFound() =>
        HttpResults.Problem(
            title: "Refunds.BuyerNotFound",
            detail: "The authenticated user does not have a buyer profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult RefundNotFound() =>
        HttpResults.Problem(
            title: "Refunds.NotFound",
            detail: "Refund was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult OrderNotFound() =>
        HttpResults.Problem(
            title: "Refunds.OrderNotFound",
            detail: "Order was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ReturnNotFound() =>
        HttpResults.Problem(
            title: "Refunds.ReturnNotFound",
            detail: "Return request was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ActorNotFound() =>
        HttpResults.Problem(
            title: "Refunds.ActorNotFound",
            detail: "The authenticated admin user id could not be read.",
            statusCode: StatusCodes.Status401Unauthorized);
}

public sealed record CreateRefundApiRequest(decimal Amount, string Reason);

public sealed record ApproveRefundApiRequest(string Reason);

public sealed record ConfirmManualProviderRefundApiRequest(string ProviderRefundReference, string Reason);
