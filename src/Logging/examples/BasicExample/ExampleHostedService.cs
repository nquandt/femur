using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Femur.Logging.Example;

internal sealed class ExampleHostedService : BackgroundService
{
    private readonly ILogger<ExampleHostedService> _logger;

    public ExampleHostedService(ILogger<ExampleHostedService> logger)
    {
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            this._logger.LogInformation("Service running at {Time}", DateTime.UtcNow);
            await Task.Delay(5000, stoppingToken);
        }
    }
}
