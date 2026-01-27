namespace Femur.Messaging;

/// <summary>
/// Represents a received message. Pure data, no settlement behavior.
/// </summary>
/// <typeparam name="T">The deserialized message body type.</typeparam>
public interface IReceivedMessage<out T> where T : class
{
    /// <summary>
    /// The deserialized message body, or throws if deserialization failed.
    /// </summary>
    T Body { get; }

    /// <summary>
    /// Whether the message body was successfully deserialized.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// The deserialization error, if any.
    /// </summary>
    Exception? DeserializationError { get; }

    /// <summary>
    /// The unique identifier for this message.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// The number of times this message has been delivered.
    /// </summary>
    int DeliveryCount { get; }

    /// <summary>
    /// The time at which the message lock expires (if applicable).
    /// </summary>
    DateTimeOffset? LockedUntil { get; }

    /// <summary>
    /// The time at which the message was enqueued.
    /// </summary>
    DateTimeOffset EnqueuedTime { get; }

    /// <summary>
    /// The correlation identifier for this message.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Custom application properties attached to the message.
    /// </summary>
    IReadOnlyDictionary<string, object> Properties { get; }
}
