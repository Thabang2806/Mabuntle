using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Swyftly.Infrastructure.Persistence;

public sealed class SwyftlyDbContextModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is not SwyftlyDbContext)
        {
            return (context.GetType(), designTime);
        }

        return (context.GetType(), context.Database.ProviderName, designTime);
    }
}
