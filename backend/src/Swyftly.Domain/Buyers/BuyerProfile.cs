using Swyftly.Domain.Common;

namespace Swyftly.Domain.Buyers;

public sealed class BuyerProfile : AuditableEntity
{
    private BuyerProfile()
    {
    }

    public BuyerProfile(Guid userId)
    {
        UserId = userId;
    }

    public Guid UserId { get; private set; }
}
