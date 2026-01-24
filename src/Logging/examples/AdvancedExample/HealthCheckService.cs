using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Femur.Logging.AdvancedExample;

public class HealthCheckService : BackgroundService
{
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(ILogger<HealthCheckService> logger)
    {
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogInformation("Health check service started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

                var memoryUsed = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                this._logger.LogInformation("Health Check - Memory: {Memory:F2} MB, Thread Pool: {Threads} threads",
                    memoryUsed,
                    ThreadPool.ThreadCount);
            }
        }
        catch (OperationCanceledException)
        {
            this._logger.LogInformation("Health check service stopping");
        }
        finally
        {
            this._logger.LogInformation("Health check service stopped");
        }
    }
}
