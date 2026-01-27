using Azure.Messaging.ServiceBus;

namespace Femur.Messaging.ServiceBus;

/// <summary>
/// Service Bus implementation of IReceivedMessage.
/// </summary>
internal sealed class ServiceBusReceivedMessage<T> : IReceivedMessage<T> where T : class
{
    private readonly ServiceBusReceivedMessage _raw;
    private readonly IMessageSerializer _serializer;
    private T? _cachedBody;
    private bool _deserialized;
    private Exception? _deserializationError;

    public ServiceBusReceivedMessage(ServiceBusReceivedMessage raw, IMessageSerializer serializer)
    {
        this._raw = raw;
        this._serializer = serializer;
        this.TryDeserialize();
    }

    public T Body
    {
        get
        {
            if (!this.IsValid)
            {
                throw new InvalidOperationException(
                    "Cannot access Body on an invalid message. Check IsValid first.",
                    this._deserializationError);
            }

            return this._cachedBody!;
        }
    }

    public bool IsValid => this._deserialized && this._deserializationError == null;

    public Exception? DeserializationError => this._deserializationError;

    public string MessageId => this._raw.MessageId;
    public int DeliveryCount => this._raw.DeliveryCount;
    public DateTimeOffset? LockedUntil => this._raw.LockedUntil;
    public DateTimeOffset EnqueuedTime => this._raw.EnqueuedTime;
    public string? CorrelationId => this._raw.CorrelationId;
    public IReadOnlyDictionary<string, object> Properties => this._raw.ApplicationProperties;

    /// <summary>
    /// Gets the underlying Service Bus message for settlement.
    /// </summary>
    internal ServiceBusReceivedMessage Raw => this._raw;

    private void TryDeserialize()
    {
        try
        {
            // Convert Azure BinaryData to ReadOnlyMemory<byte>
            var data = this._raw.Body.ToMemory();
            this._cachedBody = this._serializer.Deserialize<T>(data);
            this._deserialized = true;
        }
        catch (Exception ex)
        {
            this._deserializationError = ex;
            this._deserialized = true;
        }
    }
}
