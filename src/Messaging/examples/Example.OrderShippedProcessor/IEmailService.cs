namespace Femur.Messaging.Example.OrderShippedProcessor;

/// <summary>
/// Service for sending email notifications to customers.
/// In production, this would integrate with email providers like SendGrid, AWS SES, or Azure Communication Services.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an order shipped notification email to the customer.
    /// </summary>
    /// <param name="to">Customer's email address.</param>
    /// <param name="customerName">Customer's full name for personalization.</param>
    /// <param name="orderId">The order identifier to include in the email.</param>
    /// <param name="trackingNumber">Shipping carrier tracking number.</param>
    /// <param name="carrierName">Name of the shipping carrier (e.g., "FedEx", "UPS").</param>
    /// <param name="estimatedDelivery">Estimated delivery date.</param>
    /// <param name="orderAmount">Total order amount.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidEmailException">Thrown when the email address is invalid or cannot be delivered to.</exception>
    /// <exception cref="EmailServiceException">Thrown for transient email service errors that may be retried.</exception>
    Task SendOrderShippedEmailAsync(
        string to,
        string customerName,
        string orderId,
        string trackingNumber,
        string? carrierName,
        DateTimeOffset estimatedDelivery,
        decimal orderAmount,
        CancellationToken cancellationToken);
}

/// <summary>
/// Exception thrown when an email address is invalid or permanently undeliverable.
/// This is a permanent failure that should result in dead-lettering the message.
/// </summary>
public class InvalidEmailException : Exception
{
    public InvalidEmailException(string message) : base(message) { }
    public InvalidEmailException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown for transient email service errors (rate limits, network issues, etc.).
/// These errors are retriable and should result in message abandonment for retry.
/// </summary>
public class EmailServiceException : Exception
{
    public EmailServiceException(string message) : base(message) { }
    public EmailServiceException(string message, Exception innerException) : base(message, innerException) { }
}
