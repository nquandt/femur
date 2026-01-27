namespace Femur.Messaging;

/// <summary>
/// Implement this interface to handle messages of type T.
/// This is the only interface application code needs to implement.
/// </summary>
/// <typeparam name="T">The message type to handle.</typeparam>
public interface IMessageHandler<in T> where T : class, IMessage
{
    /// <summary>
    /// Handle the incoming message.
    /// </summary>
    /// <param name="message">The deserialized message body.</param>
    /// <param name="cancellationToken">
    /// Cancellation token that will be cancelled if the message lock is about to expire
    /// or if the service is shutting down.
    /// </param>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// <para>If this method completes successfully, the message will be completed.</para>
    /// <para>If this method throws a <see cref="DeadLetterException"/>, the message will be dead-lettered.</para>
    /// <para>If this method throws any other exception, the message will be abandoned for retry.</para>
    /// </remarks>
    Task HandleAsync(T message, CancellationToken cancellationToken);
}
