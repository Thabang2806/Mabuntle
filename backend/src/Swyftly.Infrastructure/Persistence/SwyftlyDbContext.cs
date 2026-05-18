using Microsoft.EntityFrameworkCore;

namespace Swyftly.Infrastructure.Persistence;

public sealed class SwyftlyDbContext(DbContextOptions<SwyftlyDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("swyftly");
        base.OnModelCreating(modelBuilder);
    }
}
