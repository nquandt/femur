namespace Femur.Messaging.InMemory;

/// <summary>
/// In-memory implementation of IReceivedMessage for testing.
/// </summary>
internal sealed class InMemoryReceivedMessage<T> : IReceivedMessage<T> where T : class
{
    private readonly InMemoryEnvelope _envelope;

    public InMemoryReceivedMessage(InMemoryEnvelope envelope, T body)
    {
        this._envelope = envelope;
        this.Body = body;
    }

    public InMemoryReceivedMessage(InMemoryEnvelope envelope, Exception error)
    {
        this._envelope = envelope;
        this.Body = default!;
        this.DeserializationError = error;
    }

    public T Body { get; }
    public bool IsValid => this.DeserializationError == null;
    public Exception? DeserializationError { get; }

    public string MessageId => this._envelope.MessageId;
    public int DeliveryCount => this._envelope.DeliveryCount;
    public DateTimeOffset? LockedUntil => this._envelope.LockedUntil;
    public DateTimeOffset EnqueuedTime => this._envelope.EnqueuedTime;
    public string? CorrelationId => this._envelope.CorrelationId;
    public IReadOnlyDictionary<string, object> Properties => this._envelope.Properties;

    internal InMemoryEnvelope Envelope => this._envelope;
}

/// <summary>
/// Internal envelope for tracking message state.
/// </summary>
public sealed class InMemoryEnvelope
{
    public required string MessageId { get; init; }
    public required string MessageName { get; init; }
    public required ReadOnlyMemory<byte> Body { get; init; }
    public int DeliveryCount { get; set; } = 1;
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset EnqueuedTime { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }
    public Dictionary<string, object> Properties { get; } = new();

    public InMemoryMessageState State { get; set; } = InMemoryMessageState.Pending;
    public string? DeadLetterReason { get; set; }
    public string? DeadLetterDescription { get; set; }
}

public enum InMemoryMessageState
{
    Pending,
    Processing,
    Completed,
    DeadLettered
}
