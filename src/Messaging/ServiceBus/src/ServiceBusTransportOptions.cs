using Azure.Messaging.ServiceBus;

namespace Femur.Messaging.ServiceBus;

/// <summary>
/// Configuration options for Service Bus transport.
/// </summary>
public class ServiceBusTransportOptions
{
    /// <summary>
    /// The fully qualified namespace (e.g., "mynamespace.servicebus.windows.net").
    /// Required if ConnectionString is not provided.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>
    /// The connection string. Required if FullyQualifiedNamespace is not provided.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The receive mode for messages. Default: PeekLock
    /// </summary>
    public ServiceBusReceiveMode ReceiveMode { get; set; } = ServiceBusReceiveMode.PeekLock;

    /// <summary>
    /// Maximum time to wait for a message. Default: null (uses Service Bus default)
    /// </summary>
    public TimeSpan? MaxWaitTime { get; set; }
}

/// <summary>
/// Per-message-type configuration for Service Bus.
/// </summary>
public class ServiceBusMessageOptions
{
    /// <summary>
    /// Override the queue name (otherwise uses IMessage.MessageName).
    /// </summary>
    public string? QueueName { get; set; }

    /// <summary>
    /// Use a topic instead of a queue.
    /// </summary>
    public string? TopicName { get; set; }

    /// <summary>
    /// Subscription name (required when using TopicName).
    /// </summary>
    public string? SubscriptionName { get; set; }
}
