namespace Femur.Messaging;

/// <summary>
/// Throw this exception from a message handler to explicitly dead-letter a message.
/// </summary>
public class DeadLetterException : Exception
{
    /// <summary>
    /// The reason for dead-lettering. This will be stored on the dead-lettered message.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Optional detailed description of why the message was dead-lettered.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Optional properties to add or modify on the dead-lettered message.
    /// </summary>
    public IDictionary<string, object>? PropertiesToModify { get; }

    public DeadLetterException(string reason)
        : base(reason)
    {
        this.Reason = reason;
    }

    public DeadLetterException(string reason, string? description)
        : base(description ?? reason)
    {
        this.Reason = reason;
        this.Description = description;
    }

    public DeadLetterException(string reason, string? description, IDictionary<string, object>? propertiesToModify)
        : base(description ?? reason)
    {
        this.Reason = reason;
        this.Description = description;
        this.PropertiesToModify = propertiesToModify;
    }

    public DeadLetterException(string reason, Exception innerException)
        : base(reason, innerException)
    {
        this.Reason = reason;
        this.Description = innerException.Message;
    }
}
