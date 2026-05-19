using Swyftly.Application.Admin;
using Swyftly.Domain.Admin;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Admin;

public sealed class EfAuditLogService(
    SwyftlyDbContext dbContext,
    TimeProvider timeProvider) : IAuditLogService
{
    public async Task RecordAsync(CreateAuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog(
            entry.ActorUserId,
            entry.ActorRole,
            entry.ActionType,
            entry.EntityType,
            entry.EntityId,
            timeProvider.GetUtcNow(),
            entry.PreviousValueJson,
            entry.NewValueJson,
            entry.Reason,
            entry.IpAddress));

        await Task.CompletedTask;
    }
}
