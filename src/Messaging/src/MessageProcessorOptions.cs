namespace Femur.Messaging;

/// <summary>
/// Configuration options for message processing behavior.
/// </summary>
public class MessageProcessorOptions
{
    /// <summary>
    /// Property name used to store the last exception message on abandoned/dead-lettered messages.
    /// Default: "Femur.LastException"
    /// </summary>
    public string ExceptionPropertyName { get; set; } = "Femur.LastException";

    /// <summary>
    /// Property name used to store the full exception detail on abandoned/dead-lettered messages.
    /// Default: "Femur.LastExceptionDetail"
    /// </summary>
    public string ExceptionDetailPropertyName { get; set; } = "Femur.LastExceptionDetail";

    /// <summary>
    /// Maximum length for exception detail strings. Longer strings will be truncated.
    /// Default: 2000
    /// </summary>
    public int MaxExceptionDetailLength { get; set; } = 2000;

    /// <summary>
    /// If set, messages that exceed this delivery count will be dead-lettered instead of abandoned.
    /// Leave null to use the transport's default max delivery count.
    /// </summary>
    public int? MaxDeliveryCount { get; set; }

    /// <summary>
    /// If true, creates a cancellation token that cancels when the message lock is about to expire.
    /// Default: true
    /// </summary>
    public bool EnableLockTracking { get; set; } = true;

    /// <summary>
    /// If the transport supports auto lock renewal, set this to the max renewal duration.
    /// The processing token will cancel after this duration instead of the message's LockedUntil time.
    /// Default: TimeSpan.Zero (use message lock duration)
    /// </summary>
    public TimeSpan MaxLockDuration { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Exception types that should immediately dead-letter instead of abandon for retry.
    /// Useful for exceptions that indicate the message is permanently unprocessable.
    /// </summary>
    public IReadOnlyList<Type>? DeadLetterOnExceptionTypes { get; set; }
}
