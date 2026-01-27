namespace Femur.Messaging.InMemory;

/// <summary>
/// In-memory implementation of IMessagingTransport for testing.
/// </summary>
internal sealed class InMemoryTransport : IMessagingTransport
{
    private readonly InMemoryMessageQueue _queue;

    public InMemoryTransport(InMemoryMessageQueue queue)
    {
        this._queue = queue;
    }

    public IMessageClient<T> CreateClient<T>(IMessageSerializer serializer) where T : class, IMessage
    {
        return new InMemoryMessageClient<T>(this._queue, serializer);
    }
}
