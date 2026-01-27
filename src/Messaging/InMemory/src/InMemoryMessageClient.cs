using System.Runtime.CompilerServices;

namespace Femur.Messaging.InMemory;

/// <summary>
/// In-memory implementation of IMessageClient for testing.
/// Combines receiving and settling operations in a single class.
/// </summary>
internal sealed class InMemoryMessageClient<T> : IMessageClient<T>
    where T : class, IMessage
{
    private readonly InMemoryMessageQueue _queue;
    private readonly IMessageSerializer _serializer;

    public InMemoryMessageClient(InMemoryMessageQueue queue, IMessageSerializer serializer)
    {
        this._queue = queue;
        this._serializer = serializer;
    }

    // Receiving

    public async IAsyncEnumerable<IReceivedMessage<T>> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = this._queue.GetOrCreateChannel(T.MessageName);

        await foreach (var envelope in channel.Reader.ReadAllAsync(cancellationToken))
        {
            envelope.State = InMemoryMessageState.Processing;
            envelope.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(5);

            // Deserialize outside yield statement
            IReceivedMessage<T> receivedMessage;
            try
            {
                var body = this._serializer.Deserialize<T>(envelope.Body);
                receivedMessage = new InMemoryReceivedMessage<T>(envelope, body);
            }
            catch (Exception ex)
            {
                receivedMessage = new InMemoryReceivedMessage<T>(envelope, ex);
            }

            yield return receivedMessage;
        }
    }

    public async Task<IReadOnlyList<IReceivedMessage<T>>> ReceiveBatchAsync(
        int maxMessages,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<IReceivedMessage<T>>();
        var channel = this._queue.GetOrCreateChannel(T.MessageName);

        var timeout = maxWaitTime ?? TimeSpan.FromSeconds(1);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            while (results.Count < maxMessages)
            {
                if (channel.Reader.TryRead(out var envelope))
                {
                    envelope.State = InMemoryMessageState.Processing;
                    envelope.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(5);

                    try
                    {
                        var body = this._serializer.Deserialize<T>(envelope.Body);
                        results.Add(new InMemoryReceivedMessage<T>(envelope, body));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new InMemoryReceivedMessage<T>(envelope, ex));
                    }
                }
                else
                {
                    // Wait for more messages or timeout
                    await channel.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancellation, return what we have
        }

        return results;
    }

    // Settlement

    public Task CompleteAsync(IReceivedMessage<T> message, CancellationToken cancellationToken = default)
    {
        var envelope = GetEnvelope(message);
        this._queue.Complete(envelope);
        return Task.CompletedTask;
    }

    public async Task AbandonAsync(
        IReceivedMessage<T> message,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        var envelope = GetEnvelope(message);

        if (propertiesToModify != null)
        {
            foreach (var (key, value) in propertiesToModify)
            {
                envelope.Properties[key] = value;
            }
        }

        await this._queue.RequeueAsync(envelope).ConfigureAwait(false);
    }

    public Task DeadLetterAsync(
        IReceivedMessage<T> message,
        string reason,
        string? description = null,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        var envelope = GetEnvelope(message);

        if (propertiesToModify != null)
        {
            foreach (var (key, value) in propertiesToModify)
            {
                envelope.Properties[key] = value;
            }
        }

        this._queue.DeadLetter(envelope, reason, description);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static InMemoryEnvelope GetEnvelope(IReceivedMessage<T> message)
    {
        if (message is InMemoryReceivedMessage<T> inMemoryMessage)
        {
            return inMemoryMessage.Envelope;
        }

        throw new InvalidOperationException(
            $"Message of type {message.GetType().Name} is not an in-memory message.");
    }
}
