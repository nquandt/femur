using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Femur.Messaging.ServiceBus;

/// <summary>
/// Service Bus implementation of IMessageClient.
/// Combines receiving and settling operations in a single class.
/// </summary>
internal sealed class ServiceBusMessageClient<T> : IMessageClient<T>
    where T : class, IMessage
{
    private readonly ServiceBusReceiver _receiver;
    private readonly IMessageSerializer _serializer;
    private readonly ILogger _logger;
    private readonly TimeSpan? _maxWaitTime;

    public ServiceBusMessageClient(
        ServiceBusClient client,
        IOptions<ServiceBusMessageOptions> messageOptions,
        IOptions<ServiceBusTransportOptions> transportOptions,
        IMessageSerializer serializer,
        ILogger<ServiceBusMessageClient<T>> logger)
    {
        this._logger = logger;
        this._maxWaitTime = transportOptions.Value.MaxWaitTime;
        this._serializer = serializer;

        var options = messageOptions.Value;
        var receiverOptions = new ServiceBusReceiverOptions
        {
            ReceiveMode = transportOptions.Value.ReceiveMode
        };

        // Determine queue/topic from options or static interface member
        var queueName = options.QueueName ?? T.MessageName;
        var topicName = options.TopicName;
        var subscriptionName = options.SubscriptionName;

        if (!string.IsNullOrWhiteSpace(topicName) && !string.IsNullOrWhiteSpace(subscriptionName))
        {
            this._receiver = client.CreateReceiver(topicName, subscriptionName, receiverOptions);
            this._logger.LogDebug("Created receiver for topic {TopicName} subscription {SubscriptionName}",
                topicName, subscriptionName);
        }
        else if (!string.IsNullOrWhiteSpace(queueName))
        {
            this._receiver = client.CreateReceiver(queueName, receiverOptions);
            this._logger.LogDebug("Created receiver for queue {QueueName}", queueName);
        }
        else
        {
            throw new InvalidOperationException(
                $"No queue or topic configured for message type {typeof(T).Name}. " +
                "Either configure ServiceBusMessageOptions or implement static MessageName on the message type.");
        }
    }

    // Receiving

    public async IAsyncEnumerable<IReceivedMessage<T>> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Azure.Messaging.ServiceBus.ServiceBusReceivedMessage? rawMessage;

            try
            {
                rawMessage = await this._receiver
                    .ReceiveMessageAsync(this._maxWaitTime, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                this._logger.LogDebug("Receive cancelled");
                yield break;
            }

            if (rawMessage == null)
            {
                continue;
            }

            this._logger.LogDebug("Received message {MessageId}", rawMessage.MessageId);

            yield return new ServiceBusReceivedMessage<T>(rawMessage, this._serializer);
        }
    }

    public async Task<IReadOnlyList<IReceivedMessage<T>>> ReceiveBatchAsync(
        int maxMessages,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default)
    {
        var rawMessages = await this._receiver
            .ReceiveMessagesAsync(maxMessages, maxWaitTime, cancellationToken)
            .ConfigureAwait(false);

        return rawMessages
            .Select(raw => new ServiceBusReceivedMessage<T>(raw, this._serializer))
            .ToList();
    }

    // Settlement

    public async Task CompleteAsync(IReceivedMessage<T> message, CancellationToken cancellationToken = default)
    {
        var raw = GetRawMessage(message);

        this._logger.LogDebug("Completing message {MessageId}", raw.MessageId);

        await this._receiver.CompleteMessageAsync(raw, cancellationToken).ConfigureAwait(false);
    }

    public async Task AbandonAsync(
        IReceivedMessage<T> message,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        var raw = GetRawMessage(message);

        this._logger.LogDebug("Abandoning message {MessageId}", raw.MessageId);

        await this._receiver.AbandonMessageAsync(raw, propertiesToModify, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterAsync(
        IReceivedMessage<T> message,
        string reason,
        string? description = null,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        var raw = GetRawMessage(message);

        this._logger.LogWarning("Dead-lettering message {MessageId}: {Reason}", raw.MessageId, reason);

        await this._receiver.DeadLetterMessageAsync(
            raw,
            propertiesToModify ?? new Dictionary<string, object>(),
            reason,
            description,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await this._receiver.DisposeAsync().ConfigureAwait(false);
    }

    private static Azure.Messaging.ServiceBus.ServiceBusReceivedMessage GetRawMessage(IReceivedMessage<T> message)
    {
        if (message is ServiceBusReceivedMessage<T> sbMessage)
        {
            return sbMessage.Raw;
        }

        throw new InvalidOperationException(
            $"Message of type {message.GetType().Name} is not a Service Bus message. " +
            "Cannot settle messages from different transports.");
    }
}
