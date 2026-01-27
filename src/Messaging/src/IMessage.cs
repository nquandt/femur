namespace Femur.Messaging;

/// <summary>
/// Marker interface for messages.
/// Implement this interface and provide a static MessageName.
/// </summary>
public interface IMessage
{
    /// <summary>
    /// The logical name for this message type.
    /// Transports interpret this as queue name, topic name, routing key, etc.
    /// </summary>
    static abstract string MessageName { get; }
}
