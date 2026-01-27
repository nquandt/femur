using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Femur.Messaging.InMemory;

/// <summary>
/// In-memory message queue for testing.
/// </summary>
public sealed class InMemoryMessageQueue
{
    private readonly ConcurrentDictionary<string, Channel<InMemoryEnvelope>> _queues = new();
    private readonly ConcurrentDictionary<string, List<InMemoryEnvelope>> _deadLetterQueues = new();
    private readonly ConcurrentDictionary<string, List<InMemoryEnvelope>> _completedMessages = new();

    private int _messageCounter;

    /// <summary>
    /// Publishes a message to the queue.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="serializer">The serializer to use. If null, uses default JSON serializer.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    public async Task PublishAsync<T>(T message, IMessageSerializer? serializer = null, string? correlationId = null)

        where T : class, IMessage
    {
        serializer ??= new JsonMessageSerializer();


        var envelope = new InMemoryEnvelope
        {
            MessageId = $"msg-{Interlocked.Increment(ref this._messageCounter)}",
            MessageName = T.MessageName,
            Body = serializer.Serialize(message),
            CorrelationId = correlationId,
            EnqueuedTime = DateTimeOffset.UtcNow
        };

        var channel = this.GetOrCreateChannel(T.MessageName);
        await channel.Writer.WriteAsync(envelope).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all dead-lettered messages for a queue.
    /// </summary>
    public IReadOnlyList<InMemoryEnvelope> GetDeadLetterMessages(string messageName)
    {
        return this._deadLetterQueues.TryGetValue(messageName, out var list)
            ? list.ToList()
            : [];
    }

    /// <summary>
    /// Gets all completed messages for a queue.
    /// </summary>
    public IReadOnlyList<InMemoryEnvelope> GetCompletedMessages(string messageName)
    {
        return this._completedMessages.TryGetValue(messageName, out var list)
            ? list.ToList()
            : [];
    }

    /// <summary>
    /// Clears all queues.
    /// </summary>
    public void Clear()
    {
        this._queues.Clear();
        this._deadLetterQueues.Clear();
        this._completedMessages.Clear();
    }

    internal Channel<InMemoryEnvelope> GetOrCreateChannel(string messageName)
    {
        return this._queues.GetOrAdd(messageName, _ => Channel.CreateUnbounded<InMemoryEnvelope>());
    }

    internal void DeadLetter(InMemoryEnvelope envelope, string reason, string? description)
    {
        envelope.State = InMemoryMessageState.DeadLettered;
        envelope.DeadLetterReason = reason;
        envelope.DeadLetterDescription = description;

        var list = this._deadLetterQueues.GetOrAdd(envelope.MessageName, _ => []);
        lock (list)
        {
            list.Add(envelope);
        }
    }

    internal void Complete(InMemoryEnvelope envelope)
    {
        envelope.State = InMemoryMessageState.Completed;

        var list = this._completedMessages.GetOrAdd(envelope.MessageName, _ => []);
        lock (list)
        {
            list.Add(envelope);
        }
    }

    internal async Task RequeueAsync(InMemoryEnvelope envelope)
    {
        envelope.DeliveryCount++;
        envelope.State = InMemoryMessageState.Pending;

        var channel = this.GetOrCreateChannel(envelope.MessageName);
        await channel.Writer.WriteAsync(envelope).ConfigureAwait(false);
    }
}
