using Swyftly.Domain.Notifications;

namespace Swyftly.UnitTests.Domain;

public sealed class NotificationTests
{
    [Fact]
    public void Constructor_CapturesUnreadNotification()
    {
        var recipientUserId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;

        var notification = new Notification(
            recipientUserId,
            "OrderUpdate",
            "Order shipped",
            "Your order has shipped.",
            "Order",
            Guid.NewGuid(),
            createdAtUtc);

        Assert.Equal(recipientUserId, notification.RecipientUserId);
        Assert.Equal("OrderUpdate", notification.Type);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAtUtc);
        Assert.Equal(createdAtUtc, notification.CreatedAtUtc);
    }

    [Fact]
    public void MarkRead_SetsReadTimestampOnce()
    {
        var notification = new Notification(
            Guid.NewGuid(),
            "Support",
            "New reply",
            "Support replied to your ticket.",
            null,
            null,
            DateTimeOffset.UtcNow);
        var firstReadAtUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        var secondReadAtUtc = DateTimeOffset.UtcNow.AddMinutes(2);

        notification.MarkRead(firstReadAtUtc);
        notification.MarkRead(secondReadAtUtc);

        Assert.True(notification.IsRead);
        Assert.Equal(firstReadAtUtc, notification.ReadAtUtc);
    }

    [Fact]
    public void Constructor_RejectsInvalidRequiredValues()
    {
        Assert.Throws<ArgumentException>(() => new Notification(Guid.Empty, "Type", "Title", "Message", null, null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new Notification(Guid.NewGuid(), "", "Title", "Message", null, null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new Notification(Guid.NewGuid(), "Type", "", "Message", null, null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new Notification(Guid.NewGuid(), "Type", "Title", "", null, null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new Notification(Guid.NewGuid(), "Type", "Title", "Message", null, Guid.Empty, DateTimeOffset.UtcNow));
    }
}
