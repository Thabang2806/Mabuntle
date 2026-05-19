using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Identity;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Catalog;

public static class AdminCategoryEndpoints
{
    public static IEndpointRouteBuilder MapAdminCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/categories")
            .WithTags("Admin Categories")
            .RequireAuthorization(SwyftlyPolicies.AdminOnly);

        group.MapGet("", ListAsync)
            .WithName("ListAdminCategories")
            .WithSummary("Returns category hierarchy metadata and category attribute definitions.")
            .Produces<IReadOnlyCollection<AdminCategoryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> ListAsync(
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var attributes = await dbContext.CategoryAttributes
            .OrderBy(attribute => attribute.DisplayOrder)
            .ThenBy(attribute => attribute.Name)
            .ToListAsync(cancellationToken);

        var attributesByCategory = attributes
            .GroupBy(attribute => attribute.CategoryId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(attribute => new AdminCategoryAttributeResponse(
                        attribute.Id,
                        attribute.Name,
                        attribute.Key,
                        attribute.DataType.ToString(),
                        attribute.IsRequired,
                        attribute.AllowedValues,
                        attribute.DisplayOrder,
                        attribute.IsActive))
                    .ToArray() as IReadOnlyCollection<AdminCategoryAttributeResponse>);

        var categories = await dbContext.Categories
            .OrderBy(category => category.ParentCategoryId == null ? 0 : 1)
            .ThenBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new AdminCategoryResponse(
                category.Id,
                category.ParentCategoryId,
                category.Name,
                category.Slug,
                category.DisplayOrder,
                category.IsActive,
                Array.Empty<AdminCategoryAttributeResponse>()))
            .ToListAsync(cancellationToken);

        var response = categories
            .Select(category => category with
            {
                Attributes = attributesByCategory.GetValueOrDefault(category.CategoryId)
                    ?? Array.Empty<AdminCategoryAttributeResponse>()
            })
            .ToArray();

        return HttpResults.Ok(response);
    }
}

public sealed record AdminCategoryResponse(
    Guid CategoryId,
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyCollection<AdminCategoryAttributeResponse> Attributes);

public sealed record AdminCategoryAttributeResponse(
    Guid AttributeId,
    string Name,
    string Key,
    string DataType,
    bool IsRequired,
    IReadOnlyCollection<string> AllowedValues,
    int DisplayOrder,
    bool IsActive);
