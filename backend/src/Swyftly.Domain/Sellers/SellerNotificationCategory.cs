namespace Swyftly.Domain.Sellers;

public static class SellerNotificationCategory
{
    public const string Verification = "Verification";
    public const string Products = "Products";
    public const string Revisions = "Revisions";
    public const string Ads = "Ads";

    public static readonly IReadOnlyCollection<string> All =
    [
        Verification,
        Products,
        Revisions,
        Ads
    ];

    public static bool IsSupported(string category) =>
        All.Contains(category, StringComparer.Ordinal);
}
