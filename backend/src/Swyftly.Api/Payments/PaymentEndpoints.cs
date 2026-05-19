using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Results;
using Swyftly.Api.Security;
using Swyftly.Application.Identity;
using Swyftly.Application.Payments;
using Swyftly.Domain.Buyers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Payments;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var buyerGroup = app.MapGroup("/api/payments")
            .WithTags("Payments")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        buyerGroup.MapPost("/initiate", InitiatePaymentAsync)
            .WithName("InitiatePayment")
            .WithSummary("Creates a local payment record and initializes the configured payment provider.")
            .RequireRateLimiting(SwyftlyRateLimitPolicies.Payment)
            .Produces<PaymentInitiationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/api/payments/webhook/{provider}", ProcessWebhookAsync)
            .WithTags("Payment Webhooks")
            .WithName("ProcessPaymentWebhook")
            .WithSummary("Processes a payment provider webhook with provider signature verification and idempotency.")
            .AllowAnonymous()
            .Produces<PaymentWebhookProcessingResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> InitiatePaymentAsync(
        InitiatePaymentApiRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IPaymentService paymentService,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var result = await paymentService.InitiatePaymentAsync(
            new InitiatePaymentRequest(buyer.Id, request.OrderId),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> ProcessWebhookAsync(
        string provider,
        HttpRequest httpRequest,
        IPaymentService paymentService,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(httpRequest.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var headers = httpRequest.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var result = await paymentService.ProcessWebhookAsync(
            new ProcessPaymentWebhookRequest(provider, payload, headers),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

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
            title: "Payments.BuyerNotFound",
            detail: "The authenticated user does not have a buyer profile.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record InitiatePaymentApiRequest(Guid OrderId);
