using Swyftly.Domain.Ai;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Ai;

public sealed class AiUsageLogger(
    SwyftlyDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task LogAsync(
        string featureName,
        string userId,
        Guid? sellerId,
        string modelUsed,
        int? inputTokenEstimate,
        int? outputTokenEstimate,
        decimal? costEstimate,
        int latencyMs,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        dbContext.AiUsageLogs.Add(new AiUsageLog(
            featureName,
            userId,
            sellerId,
            modelUsed,
            inputTokenEstimate,
            outputTokenEstimate,
            costEstimate,
            latencyMs,
            success,
            errorMessage,
            timeProvider.GetUtcNow()));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
