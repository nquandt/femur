namespace Femur.Messaging;

/// <summary>
/// Client for receiving and settling messages from a transport.
/// Combines message receiving and settlement operations in a single interface.
/// </summary>
/// <typeparam name="T">The message type.</typeparam>
public interface IMessageClient<T> : IAsyncDisposable where T : class, IMessage
{
    // Receiving

    /// <summary>
    /// Asynchronously receives messages as they become available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop receiving.</param>
    /// <returns>An async enumerable of received messages.</returns>
    IAsyncEnumerable<IReceivedMessage<T>> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives a batch of messages.
    /// </summary>
    /// <param name="maxMessages">Maximum number of messages to receive.</param>
    /// <param name="maxWaitTime">Maximum time to wait for messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of received messages.</returns>
    Task<IReadOnlyList<IReceivedMessage<T>>> ReceiveBatchAsync(
        int maxMessages,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default);

    // Settlement

    /// <summary>
    /// Completes the message, removing it from the queue.
    /// </summary>
    Task CompleteAsync(IReceivedMessage<T> message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abandons the message, making it available for reprocessing.
    /// </summary>
    /// <param name="message">The message to abandon.</param>
    /// <param name="propertiesToModify">Optional properties to add/modify on the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AbandonAsync(
        IReceivedMessage<T> message,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dead-letters the message, moving it to the dead-letter queue.
    /// </summary>
    /// <param name="message">The message to dead-letter.</param>
    /// <param name="reason">The reason for dead-lettering.</param>
    /// <param name="description">Optional detailed description.</param>
    /// <param name="propertiesToModify">Optional properties to add/modify on the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeadLetterAsync(
        IReceivedMessage<T> message,
        string reason,
        string? description = null,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default);
}
