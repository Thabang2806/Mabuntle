using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Swyftly.Application.Notifications;
using Swyftly.Domain.Buyers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Notifications;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.UnitTests.Infrastructure;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task CreateAsync_PublishesVisibleInAppNotification()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedBuyerAsync(dbContext);
        var publisher = new RecordingNotificationRealtimePublisher();
        var service = CreateService(dbContext, publisher);

        var result = await service.CreateAsync(new CreateNotificationRequest(
            userId,
            "OrderShipped",
            "Order shipped",
            "Your order is on the way.",
            "Order",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        Assert.NotNull(result);
        var published = Assert.Single(publisher.Created);
        Assert.Equal(result!.NotificationId, published.NotificationId);
    }

    [Fact]
    public async Task CreateAsync_DoesNotPublishHiddenEmailOnlyNotification()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedBuyerAsync(dbContext, configurePreferences: buyer =>
        {
            dbContext.BuyerNotificationPreferences.Add(new BuyerNotificationPreference(
                buyer.Id,
                BuyerNotificationCategory.Reviews,
                isEnabled: false,
                emailEnabled: true));
        });
        var publisher = new RecordingNotificationRealtimePublisher();
        var service = CreateService(dbContext, publisher);

        var result = await service.CreateAsync(new CreateNotificationRequest(
            userId,
            "ReviewApproved",
            "Review approved",
            "Your review is visible.",
            "ProductReview",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        Assert.NotNull(result);
        Assert.Empty(publisher.Created);
        Assert.Single(await dbContext.NotificationEmailDeliveries.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_SwallowsRealtimePublisherFailure()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedBuyerAsync(dbContext);
        var publisher = new RecordingNotificationRealtimePublisher { ThrowOnCreated = true };
        var service = CreateService(dbContext, publisher);

        var result = await service.CreateAsync(new CreateNotificationRequest(
            userId,
            "OrderDelivered",
            "Order delivered",
            "Your order was marked delivered.",
            "Order",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        Assert.NotNull(result);
        Assert.Single(await dbContext.Notifications.ToListAsync());
    }

    private static EfNotificationService CreateService(
        SwyftlyDbContext dbContext,
        INotificationRealtimePublisher publisher) =>
        new(
            dbContext,
            Options.Create(new EmailDeliveryOptions
            {
                FromAddress = "no-reply@swyftly.test",
                FromName = "Swyftly",
                AppBaseUrl = "http://localhost:4200"
            }),
            publisher,
            NullLogger<EfNotificationService>.Instance);

    private static async Task<Guid> SeedBuyerAsync(
        SwyftlyDbContext dbContext,
        Action<BuyerProfile>? configurePreferences = null)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"buyer-{Guid.NewGuid():N}@example.test",
            Email = $"buyer-{Guid.NewGuid():N}@example.test",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var buyer = new BuyerProfile(user.Id);
        dbContext.Users.Add(user);
        dbContext.BuyerProfiles.Add(buyer);
        await dbContext.SaveChangesAsync();
        configurePreferences?.Invoke(buyer);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static SwyftlyDbContext CreateDbContext()
    {
        var interceptor = new AuditableEntitySaveChangesInterceptor(new FixedTimeProvider(DateTimeOffset.UtcNow));
        var options = new DbContextOptionsBuilder<SwyftlyDbContext>()
            .UseInMemoryDatabase($"NotificationServiceTests-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;
        return new SwyftlyDbContext(options);
    }

    private sealed class RecordingNotificationRealtimePublisher : INotificationRealtimePublisher
    {
        public List<NotificationResult> Created { get; } = [];

        public bool ThrowOnCreated { get; init; }

        public Task PublishNotificationCreatedAsync(
            NotificationResult notification,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnCreated)
            {
                throw new InvalidOperationException("Realtime unavailable.");
            }

            Created.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishNotificationReadAsync(
            Guid recipientUserId,
            Guid notificationId,
            DateTimeOffset readAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishNotificationsReadAllAsync(
            Guid recipientUserId,
            DateTimeOffset readAtUtc,
            int updatedCount,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
