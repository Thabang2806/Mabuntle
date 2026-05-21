namespace Swyftly.Application.Notifications;

public interface INotificationService
{
    Task<NotificationResult> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateNotificationRequest(
    Guid RecipientUserId,
    string Type,
    string Title,
    string Message,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTimeOffset CreatedAtUtc);

public sealed record NotificationResult(
    Guid NotificationId,
    Guid RecipientUserId,
    string Type,
    string Title,
    string Message,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTimeOffset? ReadAtUtc,
    DateTimeOffset CreatedAtUtc);
