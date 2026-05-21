using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Results;
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

    private static IResult ActorNotFound() =>
        HttpResults.Problem(
            title: "Refunds.ActorNotFound",
            detail: "The authenticated admin user id could not be read.",
            statusCode: StatusCodes.Status401Unauthorized);
}

public sealed record CreateRefundApiRequest(decimal Amount, string Reason);

public sealed record ApproveRefundApiRequest(string Reason);

public sealed record ConfirmManualProviderRefundApiRequest(string ProviderRefundReference, string Reason);
