namespace Swyftly.Worker;

public class Worker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMinutes(1);
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Swyftly worker foundation running at {Time}", DateTimeOffset.UtcNow);
            }

            await Task.Delay(IdleDelay, stoppingToken);
        }
    }
}
