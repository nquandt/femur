using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Femur.Messaging.Example.ManualConsumption;

/// <summary>
/// Example service that manually pulls messages instead of having them automatically processed.
/// </summary>
public class ManualConsumerService : BackgroundService
{
    private readonly IMessageClient<OrderMessage> _client;
    private readonly ILogger<ManualConsumerService> _logger;

    public ManualConsumerService(
        IMessageClient<OrderMessage> client,
        ILogger<ManualConsumerService> logger)
    {
        this._client = client;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogInformation("Starting manual message consumption...");

        // Example 1: Continuous consumption with AsyncEnumerable
        await this.ConsumeStreamAsync(stoppingToken);

        // Example 2: Batch consumption (commented out)
        // await ConsumeBatchesAsync(stoppingToken);
    }

    /// <summary>
    /// Consume messages as an async stream - similar to automatic processing but YOU control the flow.
    /// </summary>
    private async Task ConsumeStreamAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in this._client.ReceiveAsync(cancellationToken))
        {
            try
            {
                if (!message.IsValid)
                {
                    this._logger.LogError(
                        message.DeserializationError,
                        "Failed to deserialize message {MessageId}",
                        message.MessageId);
                    await this._client.DeadLetterAsync(message, "DeserializationFailed", cancellationToken: cancellationToken);
                    continue;
                }

                this._logger.LogInformation(
                    "Processing order {OrderId} for {Customer} - Amount: {Amount:C}",
                    message.Body!.OrderId,
                    message.Body.CustomerName,
                    message.Body.Amount);

                // YOUR processing logic here
                await this.ProcessOrderAsync(message.Body, cancellationToken);

                // Complete the message when done
                await this._client.CompleteAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error processing message {MessageId}", message.MessageId);

                // Decide: abandon for retry or dead-letter
                if (message.DeliveryCount >= 3)
                {
                    await this._client.DeadLetterAsync(message, "MaxRetriesExceeded", ex.Message, cancellationToken: cancellationToken);
                }
                else
                {
                    await this._client.AbandonAsync(message, cancellationToken: cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Consume messages in batches - useful for batch processing scenarios.
    /// </summary>
    private async Task ConsumeBatchesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Pull a batch of up to 10 messages, wait max 30 seconds
            var batch = await this._client.ReceiveBatchAsync(
                maxMessages: 10,
                maxWaitTime: TimeSpan.FromSeconds(30),
                cancellationToken);

            if (batch.Count == 0)
            {
                this._logger.LogDebug("No messages in batch, continuing...");
                continue;
            }

            this._logger.LogInformation("Received batch of {Count} messages", batch.Count);

            // Process all messages in the batch
            var tasks = batch.Select(msg => this.ProcessMessageAsync(msg, cancellationToken));
            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessMessageAsync(IReceivedMessage<OrderMessage> message, CancellationToken cancellationToken)
    {
        try
        {
            if (message.IsValid)
            {
                await this.ProcessOrderAsync(message.Body!, cancellationToken);
                await this._client.CompleteAsync(message, cancellationToken);
            }
            else
            {
                await this._client.DeadLetterAsync(message, "InvalidMessage", cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error processing message");
            await this._client.AbandonAsync(message, cancellationToken: cancellationToken);
        }
    }

    private async Task ProcessOrderAsync(OrderMessage order, CancellationToken cancellationToken)
    {
        // Your business logic here
        await Task.Delay(100, cancellationToken); // Simulate work

        this._logger.LogInformation(
            "Order {OrderId} processed successfully",
            order.OrderId);
    }
}
