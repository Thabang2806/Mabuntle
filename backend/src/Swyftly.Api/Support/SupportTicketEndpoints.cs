using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Identity;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Domain.Support;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Support;

public static class SupportTicketEndpoints
{
    public static IEndpointRouteBuilder MapSupportTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var buyerGroup = app.MapGroup("/api/buyer/support-tickets")
            .WithTags("Buyer Support Tickets")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        buyerGroup.MapPost("", CreateBuyerTicketAsync)
            .WithName("CreateBuyerSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapGet("", GetBuyerTicketsAsync)
            .WithName("GetBuyerSupportTickets")
            .Produces<IReadOnlyCollection<SupportTicketResponse>>(StatusCodes.Status200OK);

        buyerGroup.MapGet("/{ticketId:guid}", GetBuyerTicketAsync)
            .WithName("GetBuyerSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapPost("/{ticketId:guid}/messages", AddBuyerMessageAsync)
            .WithName("AddBuyerSupportMessage")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var sellerGroup = app.MapGroup("/api/seller/support-tickets")
            .WithTags("Seller Support Tickets")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly);

        sellerGroup.MapPost("", CreateSellerTicketAsync)
            .WithName("CreateSellerSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapGet("", GetSellerTicketsAsync)
            .WithName("GetSellerSupportTickets")
            .Produces<IReadOnlyCollection<SupportTicketResponse>>(StatusCodes.Status200OK);

        sellerGroup.MapGet("/{ticketId:guid}", GetSellerTicketAsync)
            .WithName("GetSellerSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapPost("/{ticketId:guid}/messages", AddSellerMessageAsync)
            .WithName("AddSellerSupportMessage")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var supportGroup = app.MapGroup("/api/support/tickets")
            .WithTags("Support Tickets")
            .RequireAuthorization(SwyftlyPolicies.SupportAgentOnly);

        supportGroup.MapGet("", GetSupportTicketsAsync)
            .WithName("GetSupportTickets")
            .Produces<IReadOnlyCollection<SupportTicketResponse>>(StatusCodes.Status200OK);

        supportGroup.MapGet("/{ticketId:guid}", GetSupportTicketAsync)
            .WithName("GetSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        supportGroup.MapPost("/{ticketId:guid}/messages", AddSupportMessageAsync)
            .WithName("AddSupportTicketMessage")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        supportGroup.MapPost("/{ticketId:guid}/internal-notes", AddSupportInternalNoteAsync)
            .WithName("AddSupportTicketInternalNote")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        supportGroup.MapPost("/{ticketId:guid}/resolve", ResolveTicketAsync)
            .WithName("ResolveSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        supportGroup.MapPost("/{ticketId:guid}/close", CloseTicketAsync)
            .WithName("CloseSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateBuyerTicketAsync(
        CreateSupportTicketRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        if (!TryParseCategory(request.Category, out var category))
        {
            return InvalidCategory();
        }

        var validation = await ValidateBuyerLinksAsync(request, buyer.Id, dbContext, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var ticket = new SupportTicket(
            userId,
            SwyftlyRoles.Buyer,
            buyer.Id,
            null,
            category,
            request.Subject,
            request.Description,
            request.LinkedOrderId,
            request.LinkedProductId,
            request.LinkedSellerId,
            request.LinkedPaymentId,
            timeProvider.GetUtcNow());

        dbContext.SupportTickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(ticket, includeInternalMessages: false));
    }

    private static async Task<IResult> CreateSellerTicketAsync(
        CreateSupportTicketRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        if (!TryParseCategory(request.Category, out var category))
        {
            return InvalidCategory();
        }

        var validation = await ValidateSellerLinksAsync(request, seller.Id, dbContext, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var ticket = new SupportTicket(
            userId,
            SwyftlyRoles.Seller,
            null,
            seller.Id,
            category,
            request.Subject,
            request.Description,
            request.LinkedOrderId,
            request.LinkedProductId,
            request.LinkedSellerId,
            request.LinkedPaymentId,
            timeProvider.GetUtcNow());

        dbContext.SupportTickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(ticket, includeInternalMessages: false));
    }

    private static async Task<IResult> GetBuyerTicketsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var tickets = await TicketQuery(dbContext)
            .Where(ticket => ticket.BuyerId == buyer.Id)
            .OrderByDescending(ticket => ticket.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(tickets.Select(ticket => Map(ticket, includeInternalMessages: false)).ToArray());
    }

    private static async Task<IResult> GetSellerTicketsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var tickets = await TicketQuery(dbContext)
            .Where(ticket => ticket.SellerId == seller.Id)
            .OrderByDescending(ticket => ticket.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(tickets.Select(ticket => Map(ticket, includeInternalMessages: false)).ToArray());
    }

    private static async Task<IResult> GetBuyerTicketAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var ticket = await TicketQuery(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && ticket.BuyerId == buyer.Id, cancellationToken);

        return ticket is null
            ? TicketNotFound()
            : HttpResults.Ok(Map(ticket, includeInternalMessages: false));
    }

    private static async Task<IResult> GetSellerTicketAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var ticket = await TicketQuery(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && ticket.SellerId == seller.Id, cancellationToken);

        return ticket is null
            ? TicketNotFound()
            : HttpResults.Ok(Map(ticket, includeInternalMessages: false));
    }

    private static async Task<IResult> GetSupportTicketsAsync(
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tickets = await TicketQuery(dbContext)
            .OrderByDescending(ticket => ticket.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(tickets.Select(ticket => Map(ticket, includeInternalMessages: true)).ToArray());
    }

    private static async Task<IResult> GetSupportTicketAsync(
        Guid ticketId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ticket = await TicketQuery(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);

        return ticket is null
            ? TicketNotFound()
            : HttpResults.Ok(Map(ticket, includeInternalMessages: true));
    }

    private static async Task<IResult> AddBuyerMessageAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        return await AddCustomerMessageAsync(ticketId, request, principal, SwyftlyRoles.Buyer, buyerId: buyer.Id, sellerId: null, dbContext, timeProvider, cancellationToken);
    }

    private static async Task<IResult> AddSellerMessageAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        return await AddCustomerMessageAsync(ticketId, request, principal, SwyftlyRoles.Seller, buyerId: null, sellerId: seller.Id, dbContext, timeProvider, cancellationToken);
    }

    private static async Task<IResult> AddCustomerMessageAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        string role,
        Guid? buyerId,
        Guid? sellerId,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(
                ticket => ticket.Id == ticketId
                    && (buyerId == null || ticket.BuyerId == buyerId)
                    && (sellerId == null || ticket.SellerId == sellerId),
                cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        try
        {
            ticket.AddCustomerMessage(userId, role, request.Message, timeProvider.GetUtcNow());
            dbContext.SupportMessages.Add(ticket.Messages.OrderBy(message => message.CreatedAtUtc).Last());
            await dbContext.SaveChangesAsync(cancellationToken);
            return HttpResults.Ok(Map(ticket, includeInternalMessages: false));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.ParamName ?? "message", exception.Message);
        }
    }

    private static async Task<IResult> AddSupportMessageAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await AddSupportTicketMessageAsync(
            ticketId,
            request,
            principal,
            isInternal: false,
            dbContext,
            timeProvider,
            cancellationToken);
    }

    private static async Task<IResult> AddSupportInternalNoteAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await AddSupportTicketMessageAsync(
            ticketId,
            request,
            principal,
            isInternal: true,
            dbContext,
            timeProvider,
            cancellationToken);
    }

    private static async Task<IResult> AddSupportTicketMessageAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        bool isInternal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        try
        {
            if (isInternal)
            {
                ticket.AddInternalNote(userId, GetSupportActorRole(principal), request.Message, timeProvider.GetUtcNow());
            }
            else
            {
                ticket.AddSupportResponse(userId, GetSupportActorRole(principal), request.Message, timeProvider.GetUtcNow());
            }

            dbContext.SupportMessages.Add(ticket.Messages.OrderBy(message => message.CreatedAtUtc).Last());
            await dbContext.SaveChangesAsync(cancellationToken);
            return HttpResults.Ok(Map(ticket, includeInternalMessages: true));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.ParamName ?? "message", exception.Message);
        }
    }

    private static async Task<IResult> ResolveTicketAsync(
        Guid ticketId,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        try
        {
            ticket.Resolve(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            return HttpResults.Ok(Map(ticket, includeInternalMessages: true));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    private static async Task<IResult> CloseTicketAsync(
        Guid ticketId,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        ticket.Close(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(Map(ticket, includeInternalMessages: true));
    }

    private static async Task<IResult?> ValidateBuyerLinksAsync(
        CreateSupportTicketRequest request,
        Guid buyerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.LinkedOrderId.HasValue)
        {
            var orderExists = await dbContext.Orders
                .AnyAsync(order => order.Id == request.LinkedOrderId && order.BuyerId == buyerId, cancellationToken);
            if (!orderExists)
            {
                return LinkedRecordNotFound("Order");
            }
        }

        if (request.LinkedPaymentId.HasValue)
        {
            var paymentExists = await dbContext.Payments
                .AnyAsync(payment => payment.Id == request.LinkedPaymentId && payment.BuyerId == buyerId, cancellationToken);
            if (!paymentExists)
            {
                return LinkedRecordNotFound("Payment");
            }
        }

        return await ValidateSharedLinksAsync(request, dbContext, cancellationToken);
    }

    private static async Task<IResult?> ValidateSellerLinksAsync(
        CreateSupportTicketRequest request,
        Guid sellerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.LinkedOrderId.HasValue)
        {
            var orderExists = await dbContext.Orders
                .AnyAsync(order => order.Id == request.LinkedOrderId && order.SellerId == sellerId, cancellationToken);
            if (!orderExists)
            {
                return LinkedRecordNotFound("Order");
            }
        }

        if (request.LinkedProductId.HasValue)
        {
            var productExists = await dbContext.Products
                .AnyAsync(product => product.Id == request.LinkedProductId && product.SellerId == sellerId, cancellationToken);
            if (!productExists)
            {
                return LinkedRecordNotFound("Product");
            }
        }

        if (request.LinkedSellerId.HasValue && request.LinkedSellerId != sellerId)
        {
            return LinkedRecordNotFound("Seller");
        }

        if (request.LinkedPaymentId.HasValue)
        {
            var paymentExists = await dbContext.Payments
                .Join(
                    dbContext.Orders,
                    payment => payment.OrderId,
                    order => order.Id,
                    (payment, order) => new { payment, order })
                .AnyAsync(item => item.payment.Id == request.LinkedPaymentId && item.order.SellerId == sellerId, cancellationToken);
            if (!paymentExists)
            {
                return LinkedRecordNotFound("Payment");
            }
        }

        return await ValidateSharedLinksAsync(request, dbContext, cancellationToken);
    }

    private static async Task<IResult?> ValidateSharedLinksAsync(
        CreateSupportTicketRequest request,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.LinkedProductId.HasValue)
        {
            var productExists = await dbContext.Products.AnyAsync(product => product.Id == request.LinkedProductId, cancellationToken);
            if (!productExists)
            {
                return LinkedRecordNotFound("Product");
            }
        }

        if (request.LinkedSellerId.HasValue)
        {
            var sellerExists = await dbContext.SellerProfiles.AnyAsync(seller => seller.Id == request.LinkedSellerId, cancellationToken);
            if (!sellerExists)
            {
                return LinkedRecordNotFound("Seller");
            }
        }

        return null;
    }

    private static IQueryable<SupportTicket> TicketQuery(SwyftlyDbContext dbContext) =>
        dbContext.SupportTickets
            .Include(ticket => ticket.Messages)
            .AsNoTracking();

    private static IQueryable<SupportTicket> TicketQueryForUpdate(SwyftlyDbContext dbContext) =>
        dbContext.SupportTickets.Include(ticket => ticket.Messages);

    private static SupportTicketResponse Map(SupportTicket ticket, bool includeInternalMessages) =>
        new(
            ticket.Id,
            ticket.CreatedByUserId,
            ticket.CreatedByRole,
            ticket.BuyerId,
            ticket.SellerId,
            ticket.Category.ToString(),
            ticket.Status.ToString(),
            ticket.Subject,
            ticket.Description,
            ticket.LinkedOrderId,
            ticket.LinkedProductId,
            ticket.LinkedSellerId,
            ticket.LinkedPaymentId,
            ticket.AssignedSupportUserId,
            ticket.OpenedAtUtc,
            ticket.ResolvedAtUtc,
            ticket.ClosedAtUtc,
            ticket.Messages
                .Where(message => includeInternalMessages || !message.IsInternal)
                .OrderBy(message => message.CreatedAtUtc)
                .Select(message => new SupportMessageResponse(
                    message.Id,
                    message.SenderUserId,
                    message.SenderRole,
                    message.Message,
                    message.IsInternal,
                    message.CreatedAtUtc))
                .ToArray());

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

    private static string GetSupportActorRole(ClaimsPrincipal principal)
    {
        if (principal.IsInRole(SwyftlyRoles.SuperAdmin))
        {
            return SwyftlyRoles.SuperAdmin;
        }

        return principal.IsInRole(SwyftlyRoles.Admin)
            ? SwyftlyRoles.Admin
            : SwyftlyRoles.SupportAgent;
    }

    private static bool TryParseCategory(string category, out SupportTicketCategory parsed) =>
        Enum.TryParse(category, ignoreCase: true, out parsed);

    private static IResult InvalidCategory() =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["category"] = [$"Category must be one of: {string.Join(", ", Enum.GetNames<SupportTicketCategory>())}."]
        });

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult Conflict(string detail) =>
        HttpResults.Problem(title: "SupportTickets.InvalidState", detail: detail, statusCode: StatusCodes.Status409Conflict);

    private static IResult LinkedRecordNotFound(string recordType) =>
        HttpResults.Problem(
            title: $"SupportTickets.Linked{recordType}NotFound",
            detail: $"Linked {recordType.ToLowerInvariant()} was not found or is not available to the authenticated user.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult BuyerNotFound() =>
        HttpResults.Problem("The authenticated user does not have a buyer profile.", statusCode: StatusCodes.Status404NotFound, title: "SupportTickets.BuyerNotFound");

    private static IResult SellerNotFound() =>
        HttpResults.Problem("The authenticated user does not have a seller profile.", statusCode: StatusCodes.Status404NotFound, title: "SupportTickets.SellerNotFound");

    private static IResult TicketNotFound() =>
        HttpResults.Problem("Support ticket was not found.", statusCode: StatusCodes.Status404NotFound, title: "SupportTickets.TicketNotFound");

    private static IResult UserNotFound() =>
        HttpResults.Problem("The authenticated user id could not be resolved.", statusCode: StatusCodes.Status404NotFound, title: "SupportTickets.UserNotFound");
}

public sealed record CreateSupportTicketRequest(
    string Category,
    string Subject,
    string Description,
    Guid? LinkedOrderId,
    Guid? LinkedProductId,
    Guid? LinkedSellerId,
    Guid? LinkedPaymentId);

public sealed record SupportMessageRequest(string Message);

public sealed record SupportTicketResponse(
    Guid SupportTicketId,
    Guid CreatedByUserId,
    string CreatedByRole,
    Guid? BuyerId,
    Guid? SellerId,
    string Category,
    string Status,
    string Subject,
    string Description,
    Guid? LinkedOrderId,
    Guid? LinkedProductId,
    Guid? LinkedSellerId,
    Guid? LinkedPaymentId,
    Guid? AssignedSupportUserId,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    IReadOnlyCollection<SupportMessageResponse> Messages);

public sealed record SupportMessageResponse(
    Guid SupportMessageId,
    Guid SenderUserId,
    string SenderRole,
    string Message,
    bool IsInternal,
    DateTimeOffset CreatedAtUtc);
