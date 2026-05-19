using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Identity;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Carts;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cart")
            .WithTags("Cart")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        group.MapGet("", GetCartAsync)
            .WithName("GetCart")
            .WithSummary("Returns the active cart for the authenticated buyer.")
            .Produces<CartResponse>(StatusCodes.Status200OK);

        group.MapPost("/items", AddItemAsync)
            .WithName("AddCartItem")
            .WithSummary("Adds a published product variant to the authenticated buyer's active cart.")
            .Produces<CartResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/items/{itemId:guid}", UpdateItemAsync)
            .WithName("UpdateCartItem")
            .WithSummary("Updates a cart item quantity.")
            .Produces<CartResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/items/{itemId:guid}", DeleteItemAsync)
            .WithName("DeleteCartItem")
            .WithSummary("Removes an item from the authenticated buyer's active cart.")
            .Produces<CartResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("", ClearCartAsync)
            .WithName("ClearCart")
            .WithSummary("Clears the authenticated buyer's active cart.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetCartAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        return HttpResults.Ok(await CreateCartResponseAsync(cart, dbContext, cancellationToken));
    }

    private static async Task<IResult> AddItemAsync(
        AddCartItemRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var productVariant = await dbContext.ProductVariants.SingleOrDefaultAsync(
            variant => variant.Id == request.ProductVariantId,
            cancellationToken);
        if (productVariant is null)
        {
            return VariantNotFound();
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == productVariant.ProductId,
            cancellationToken);
        if (product is null || product.Status != ProductStatus.Published)
        {
            return ProductNotAvailable();
        }

        if (productVariant.Status != ProductVariantStatus.Active)
        {
            return Validation("productVariantId", "Product variant is not available.");
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is null)
        {
            cart = new Cart(buyer.Id);
            dbContext.Carts.Add(cart);
        }

        try
        {
            cart.AddOrUpdateItem(
                product.Id,
                productVariant.Id,
                product.SellerId,
                product.Title,
                productVariant.Sku,
                productVariant.Size,
                productVariant.Colour,
                productVariant.Price,
                request.Quantity,
                productVariant.AvailableQuantity);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Validation("cart", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateCartResponseAsync(cart, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateItemAsync(
        Guid itemId,
        UpdateCartItemRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is null || cart.Items.All(item => item.Id != itemId))
        {
            return CartItemNotFound();
        }

        var cartItem = cart.Items.Single(item => item.Id == itemId);
        var variant = await dbContext.ProductVariants.SingleAsync(
            variant => variant.Id == cartItem.ProductVariantId,
            cancellationToken);

        try
        {
            cart.SetItemQuantity(itemId, request.Quantity, variant.AvailableQuantity);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Validation("quantity", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateCartResponseAsync(cart, dbContext, cancellationToken));
    }

    private static async Task<IResult> DeleteItemAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is null || cart.Items.All(item => item.Id != itemId))
        {
            return CartItemNotFound();
        }

        try
        {
            cart.RemoveItem(itemId);
        }
        catch (InvalidOperationException exception)
        {
            return Validation("cart", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateCartResponseAsync(cart, dbContext, cancellationToken));
    }

    private static async Task<IResult> ClearCartAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is not null)
        {
            dbContext.Carts.Remove(cart);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return HttpResults.NoContent();
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

    private static async Task<Cart?> GetActiveCartAsync(
        Guid buyerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.Carts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.BuyerId == buyerId && cart.Status == CartStatus.Active,
                cancellationToken);

    private static async Task<CartResponse> CreateCartResponseAsync(
        Cart? cart,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (cart is null)
        {
            return CartResponse.Empty;
        }

        var sellerStoreName = cart.SellerId.HasValue
            ? await dbContext.SellerStorefronts
                .Where(storefront => storefront.SellerId == cart.SellerId.Value)
                .Select(storefront => storefront.StoreName)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var items = cart.Items
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => new CartItemResponse(
                item.Id,
                item.ProductId,
                item.ProductVariantId,
                item.ProductTitle,
                item.Sku,
                item.Size,
                item.Colour,
                item.UnitPrice,
                item.Quantity,
                item.LineTotal))
            .ToArray();

        return new CartResponse(
            cart.Id,
            cart.BuyerId,
            cart.SellerId,
            sellerStoreName,
            items,
            cart.TotalQuantity,
            cart.Subtotal);
    }

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult BuyerNotFound() =>
        HttpResults.Problem(
            title: "Cart.BuyerNotFound",
            detail: "The authenticated user does not have a buyer profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult VariantNotFound() =>
        HttpResults.Problem(
            title: "Cart.ProductVariantNotFound",
            detail: "Product variant was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult CartItemNotFound() =>
        HttpResults.Problem(
            title: "Cart.ItemNotFound",
            detail: "Cart item was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ProductNotAvailable() =>
        HttpResults.Problem(
            title: "Cart.ProductNotAvailable",
            detail: "Product is not available for purchase.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record AddCartItemRequest(
    Guid ProductVariantId,
    int Quantity);

public sealed record UpdateCartItemRequest(int Quantity);

public sealed record CartResponse(
    Guid? CartId,
    Guid? BuyerId,
    Guid? SellerId,
    string? SellerStoreName,
    IReadOnlyCollection<CartItemResponse> Items,
    int TotalQuantity,
    decimal Subtotal)
{
    public static CartResponse Empty => new(null, null, null, null, [], 0, 0);
}

public sealed record CartItemResponse(
    Guid CartItemId,
    Guid ProductId,
    Guid ProductVariantId,
    string? ProductTitle,
    string Sku,
    string Size,
    string Colour,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
