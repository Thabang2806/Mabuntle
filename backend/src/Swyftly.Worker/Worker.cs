namespace Swyftly.Worker;

using Swyftly.Application.Inventory;

public class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Swyftly worker expiring inventory reservations at {Time}", timeProvider.GetUtcNow());
            }

            await ExpireReservationsAsync(stoppingToken);
            await Task.Delay(IdleDelay, stoppingToken);
        }
    }

    private async Task ExpireReservationsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var reservationService = scope.ServiceProvider.GetRequiredService<IInventoryReservationService>();
            var result = await reservationService.ExpireReservationsAsync(timeProvider.GetUtcNow(), stoppingToken);

            if (result.IsSuccess && result.Value.Count > 0)
            {
                logger.LogInformation("Expired {Count} inventory reservations.", result.Value.Count);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Inventory reservation expiry placeholder failed.");
        }
    }
}
