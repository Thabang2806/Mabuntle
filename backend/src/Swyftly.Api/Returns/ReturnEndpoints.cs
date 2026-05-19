using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Results;
using Swyftly.Application.Identity;
using Swyftly.Application.Returns;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Returns;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using Swyftly.Infrastructure.Returns;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Returns;

public static class ReturnEndpoints
{
    public static IEndpointRouteBuilder MapReturnEndpoints(this IEndpointRouteBuilder app)
    {
        var buyerGroup = app.MapGroup("/api/buyer")
            .WithTags("Buyer Returns")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        buyerGroup.MapPost("/orders/{orderId:guid}/returns", CreateReturnAsync)
            .WithName("CreateBuyerReturn")
            .WithSummary("Creates a return request for a delivered buyer order.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        buyerGroup.MapGet("/returns", GetBuyerReturnsAsync)
            .WithName("GetBuyerReturns")
            .WithSummary("Returns return requests owned by the authenticated buyer.")
            .Produces<IReadOnlyCollection<ReturnRequestResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapGet("/returns/{returnRequestId:guid}", GetBuyerReturnAsync)
            .WithName("GetBuyerReturn")
            .WithSummary("Returns one return request owned by the authenticated buyer.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapPost("/returns/{returnRequestId:guid}/dispute", DisputeReturnAsync)
            .WithName("DisputeBuyerReturn")
            .WithSummary("Escalates a rejected buyer return to dispute status.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var sellerGroup = app.MapGroup("/api/seller/returns")
            .WithTags("Seller Returns")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly);

        sellerGroup.MapGet("", GetSellerReturnsAsync)
            .WithName("GetSellerReturns")
            .WithSummary("Returns return requests for the authenticated seller.")
            .Produces<IReadOnlyCollection<ReturnRequestResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapGet("/{returnRequestId:guid}", GetSellerReturnAsync)
            .WithName("GetSellerReturn")
            .WithSummary("Returns one return request for the authenticated seller.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapPost("/{returnRequestId:guid}/approve", ApproveReturnAsync)
            .WithName("ApproveSellerReturn")
            .WithSummary("Approves a return request awaiting seller response.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sellerGroup.MapPost("/{returnRequestId:guid}/reject", RejectReturnAsync)
            .WithName("RejectSellerReturn")
            .WithSummary("Rejects a return request awaiting seller response.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var adminGroup = app.MapGroup("/api/admin/returns")
            .WithTags("Admin Returns")
            .RequireAuthorization(SwyftlyPolicies.AdminOnly);

        adminGroup.MapGet("/disputed", GetDisputedReturnsAsync)
            .WithName("GetDisputedReturns")
            .WithSummary("Returns disputed return requests for admin review.")
            .Produces<IReadOnlyCollection<ReturnRequestResult>>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> CreateReturnAsync(
        Guid orderId,
        CreateReturnRequestApiRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IReturnWorkflowService returnWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        if (!TryGetUserId(principal, out var buyerUserId))
        {
            return UserNotFound();
        }

        var result = await returnWorkflowService.RequestReturnAsync(
            new CreateReturnRequest(
                buyer.Id,
                buyerUserId,
                orderId,
                request.Reason,
                request.Details,
                request.Items.Select(item => new CreateReturnItemRequest(
                    item.OrderItemId,
                    item.Quantity,
                    item.Reason,
                    item.IsOpenedOrUnsealed,
                    item.Note)).ToArray(),
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> GetBuyerReturnsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var returns = await ReturnQuery(dbContext)
            .Where(returnRequest => returnRequest.BuyerId == buyer.Id)
            .OrderByDescending(returnRequest => returnRequest.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(returns.Select(EfReturnWorkflowService.Map).ToArray());
    }

    private static async Task<IResult> GetBuyerReturnAsync(
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

        var returnRequest = await ReturnQuery(dbContext)
            .SingleOrDefaultAsync(
                existing => existing.Id == returnRequestId && existing.BuyerId == buyer.Id,
                cancellationToken);

        return returnRequest is null
            ? ReturnNotFound()
            : HttpResults.Ok(EfReturnWorkflowService.Map(returnRequest));
    }

    private static async Task<IResult> DisputeReturnAsync(
        Guid returnRequestId,
        DisputeReturnRequestApiRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IReturnWorkflowService returnWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        if (!TryGetUserId(principal, out var buyerUserId))
        {
            return UserNotFound();
        }

        var result = await returnWorkflowService.DisputeReturnAsync(
            new BuyerReturnDisputeRequest(
                buyer.Id,
                buyerUserId,
                returnRequestId,
                request.Reason,
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> GetSellerReturnsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var returns = await ReturnQuery(dbContext)
            .Where(returnRequest => returnRequest.SellerId == seller.Id)
            .OrderByDescending(returnRequest => returnRequest.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(returns.Select(EfReturnWorkflowService.Map).ToArray());
    }

    private static async Task<IResult> GetSellerReturnAsync(
        Guid returnRequestId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var returnRequest = await ReturnQuery(dbContext)
            .SingleOrDefaultAsync(
                existing => existing.Id == returnRequestId && existing.SellerId == seller.Id,
                cancellationToken);

        return returnRequest is null
            ? ReturnNotFound()
            : HttpResults.Ok(EfReturnWorkflowService.Map(returnRequest));
    }

    private static async Task<IResult> ApproveReturnAsync(
        Guid returnRequestId,
        SellerReturnResponseApiRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IReturnWorkflowService returnWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (!TryGetUserId(principal, out var sellerUserId))
        {
            return UserNotFound();
        }

        var result = await returnWorkflowService.ApproveReturnAsync(
            new SellerReturnResponseRequest(
                seller.Id,
                sellerUserId,
                returnRequestId,
                request.Message,
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> RejectReturnAsync(
        Guid returnRequestId,
        SellerReturnResponseApiRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IReturnWorkflowService returnWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (!TryGetUserId(principal, out var sellerUserId))
        {
            return UserNotFound();
        }

        var result = await returnWorkflowService.RejectReturnAsync(
            new SellerReturnResponseRequest(
                seller.Id,
                sellerUserId,
                returnRequestId,
                request.Message,
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> GetDisputedReturnsAsync(
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var returns = await ReturnQuery(dbContext)
            .Where(returnRequest => returnRequest.Status == ReturnStatus.Disputed)
            .OrderBy(returnRequest => returnRequest.DisputedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(returns.Select(EfReturnWorkflowService.Map).ToArray());
    }

    private static IQueryable<ReturnRequest> ReturnQuery(SwyftlyDbContext dbContext) =>
        dbContext.ReturnRequests
            .Include(returnRequest => returnRequest.Items)
            .Include(returnRequest => returnRequest.Messages)
            .AsNoTracking();

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

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }

    private static IResult BuyerNotFound() =>
        HttpResults.Problem(
            title: "Returns.BuyerNotFound",
            detail: "The authenticated user does not have a buyer profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "Returns.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult UserNotFound() =>
        HttpResults.Problem(
            title: "Returns.UserNotFound",
            detail: "The authenticated user id could not be resolved.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ReturnNotFound() =>
        HttpResults.Problem(
            title: "Returns.NotFound",
            detail: "Return request was not found.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record CreateReturnRequestApiRequest(
    string Reason,
    string? Details,
    IReadOnlyCollection<CreateReturnItemApiRequest> Items);

public sealed record CreateReturnItemApiRequest(
    Guid OrderItemId,
    int Quantity,
    string Reason,
    bool IsOpenedOrUnsealed,
    string? Note);

public sealed record SellerReturnResponseApiRequest(string? Message);

public sealed record DisputeReturnRequestApiRequest(string Reason);
