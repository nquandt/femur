using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Femur.Messaging;

/// <summary>
/// BackgroundService that runs a message processor.
/// </summary>
public sealed class MessageProcessorHostedService<T> : BackgroundService
    where T : class, IMessage
{
    private readonly MessageProcessor<T> _processor;
    private readonly ILogger<MessageProcessorHostedService<T>> _logger;

    public MessageProcessorHostedService(
        MessageProcessor<T> processor,
        ILogger<MessageProcessorHostedService<T>> logger)
    {
        this._processor = processor;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var messageType = typeof(T).Name;

        this._logger.LogInformation("Starting hosted service for {MessageType}", messageType);

        try
        {
            await this._processor.ProcessAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            this._logger.LogInformation("Hosted service for {MessageType} shutdown requested", messageType);
        }
        catch (Exception ex)
        {
            this._logger.LogCritical(ex, "Hosted service for {MessageType} crashed", messageType);
            throw;
        }

        this._logger.LogInformation("Hosted service for {MessageType} stopped", messageType);
    }
}
