using Swyftly.Application.Notifications;
using Swyftly.Domain.Notifications;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Notifications;

public sealed class EfNotificationService(SwyftlyDbContext dbContext) : INotificationService
{
    public async Task<NotificationResult> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification(
            request.RecipientUserId,
            request.Type,
            request.Title,
            request.Message,
            request.RelatedEntityType,
            request.RelatedEntityId,
            request.CreatedAtUtc);

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(notification);
    }

    public static NotificationResult Map(Notification notification) =>
        new(
            notification.Id,
            notification.RecipientUserId,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.ReadAtUtc,
            notification.CreatedAtUtc);
}
