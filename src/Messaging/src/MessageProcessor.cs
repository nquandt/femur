using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Femur.Messaging;

/// <summary>
/// Orchestrates message processing by coordinating the client and handler.
/// </summary>
public sealed class MessageProcessor<T> where T : class, IMessage
{
    private readonly IMessageClient<T> _client;
    private readonly IMessageHandler<T> _handler;
    private readonly ILogger<MessageProcessor<T>> _logger;
    private readonly MessageProcessorOptions _options;

    public MessageProcessor(
        IMessageClient<T> client,
        IMessageHandler<T> handler,
        ILogger<MessageProcessor<T>> logger,
        IOptions<MessageProcessorOptions>? options = null)
    {
        this._client = client;
        this._handler = handler;
        this._logger = logger;
        this._options = options?.Value ?? new MessageProcessorOptions();
    }

    /// <summary>
    /// Starts processing messages until cancellation is requested.
    /// </summary>
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var messageType = typeof(T).Name;

        this._logger.LogInformation("Starting processor for {MessageType}", messageType);

        try
        {
            await foreach (var message in this._client.ReceiveAsync(cancellationToken))
            {
                await this.ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this._logger.LogInformation("Processor for {MessageType} cancellation requested", messageType);
        }

        this._logger.LogInformation("Processor for {MessageType} stopped", messageType);
    }

    private async Task ProcessMessageAsync(IReceivedMessage<T> message, CancellationToken cancellationToken)
    {
        using var scope = this._logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
            ["DeliveryCount"] = message.DeliveryCount,
            ["CorrelationId"] = message.CorrelationId ?? "(none)"
        });

        // Handle deserialization failures
        if (!message.IsValid)
        {
            this._logger.LogError(message.DeserializationError,
                "Failed to deserialize message {MessageId}, dead-lettering",
                message.MessageId);

            await this._client.DeadLetterAsync(
                message,
                "DeserializationFailed",
                message.DeserializationError?.Message,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
            return;
        }

        this._logger.LogDebug("Processing message {MessageId}, delivery {DeliveryCount}",
            message.MessageId, message.DeliveryCount);

        var processingToken = this.CreateProcessingToken(message, cancellationToken);

        try
        {
            await this._handler.HandleAsync(message.Body, processingToken).ConfigureAwait(false);

            // Check if we ran out of time
            if (processingToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                this._logger.LogWarning(
                    "Processing token cancelled (likely lock expired) for message {MessageId}, abandoning",
                    message.MessageId);

                await this.AbandonWithExceptionAsync(
                    message,
                    new TimeoutException("Message processing exceeded lock duration")).ConfigureAwait(false);
                return;
            }

            // Service is shutting down
            if (cancellationToken.IsCancellationRequested)
            {
                this._logger.LogWarning("Service cancellation requested, abandoning message {MessageId}",
                    message.MessageId);

                await this._client.AbandonAsync(message, cancellationToken: CancellationToken.None)
                    .ConfigureAwait(false);
                return;
            }

            // Success
            await this._client.CompleteAsync(message, cancellationToken).ConfigureAwait(false);
            this._logger.LogDebug("Message {MessageId} completed successfully", message.MessageId);
        }
        catch (DeadLetterException ex)
        {
            this._logger.LogWarning(ex, "Handler requested dead-letter for message {MessageId}: {Reason}",
                message.MessageId, ex.Reason);

            await this._client.DeadLetterAsync(
                message,
                ex.Reason,
                ex.Description,
                ex.PropertiesToModify,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (this.ShouldDeadLetter(message, ex))
        {
            this._logger.LogError(ex,
                "Max delivery attempts reached or non-retryable exception for message {MessageId}, dead-lettering",
                message.MessageId);

            await this.DeadLetterWithExceptionAsync(message, ex).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Processing failed for message {MessageId}, abandoning for retry",
                message.MessageId);

            await this.AbandonWithExceptionAsync(message, ex).ConfigureAwait(false);
        }
    }

    private bool ShouldDeadLetter(IReceivedMessage<T> message, Exception ex)
    {
        if (this._options.MaxDeliveryCount.HasValue && message.DeliveryCount >= this._options.MaxDeliveryCount.Value)
        {
            return true;
        }

        if (this._options.DeadLetterOnExceptionTypes?.Any(t => t.IsInstanceOfType(ex)) == true)
        {
            return true;
        }

        return false;
    }

    private async Task AbandonWithExceptionAsync(IReceivedMessage<T> message, Exception ex)
    {
        var properties = new Dictionary<string, object>
        {
            [this._options.ExceptionPropertyName] = ex.Message,
            [this._options.ExceptionDetailPropertyName] = Truncate(ex.ToString(), this._options.MaxExceptionDetailLength)
        };

        await this._client.AbandonAsync(message, properties, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task DeadLetterWithExceptionAsync(IReceivedMessage<T> message, Exception ex)
    {
        var properties = new Dictionary<string, object>
        {
            [this._options.ExceptionPropertyName] = ex.Message,
            [this._options.ExceptionDetailPropertyName] = Truncate(ex.ToString(), this._options.MaxExceptionDetailLength)
        };

        var reason = this._options.MaxDeliveryCount.HasValue && message.DeliveryCount >= this._options.MaxDeliveryCount.Value
            ? "MaxDeliveryCountExceeded"
            : ex.GetType().Name;

        await this._client.DeadLetterAsync(
            message,
            reason,
            ex.Message,
            properties,
            CancellationToken.None).ConfigureAwait(false);
    }

    private CancellationToken CreateProcessingToken(IReceivedMessage<T> message, CancellationToken serviceCancellation)
    {
        if (!this._options.EnableLockTracking || !message.LockedUntil.HasValue)
        {
            return serviceCancellation;
        }

        TimeSpan timeout;

        if (this._options.MaxLockDuration > TimeSpan.Zero)
        {
            timeout = this._options.MaxLockDuration;
        }
        else
        {
            timeout = message.LockedUntil.Value - DateTimeOffset.UtcNow;

            if (timeout.TotalMilliseconds <= 0)
            {
                return new CancellationToken(true);
            }
        }

        var lockCts = new CancellationTokenSource(timeout);
        return CancellationTokenSource.CreateLinkedTokenSource(lockCts.Token, serviceCancellation).Token;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
