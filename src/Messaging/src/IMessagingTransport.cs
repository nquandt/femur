namespace Femur.Messaging;

/// <summary>
/// Factory for creating transport-specific message clients.
/// Each transport (Service Bus, RabbitMQ, etc.) implements this interface.
/// </summary>
public interface IMessagingTransport
{
    /// <summary>
    /// Creates a client for receiving and settling messages of the specified type.
    /// </summary>
    /// <param name="serializer">The serializer to use for message bodies.</param>
    IMessageClient<T> CreateClient<T>(IMessageSerializer serializer) where T : class, IMessage;
}
