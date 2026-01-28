using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Femur.Messaging.Example.OrderShippedProcessor;

/// <summary>
/// Mock implementation of IEmailService for demonstration purposes.
/// In production, replace this with a real email service integration (SendGrid, AWS SES, etc.).
/// </summary>
public class MockEmailService : IEmailService
{
    private readonly ILogger<MockEmailService> _logger;
    private readonly MockEmailServiceOptions _options;

    public MockEmailService(
        ILogger<MockEmailService> logger,
        IOptions<MockEmailServiceOptions> options)
    {
        this._logger = logger;
        this._options = options.Value;
    }

    public async Task SendOrderShippedEmailAsync(
        string to,
        string customerName,
        string orderId,
        string trackingNumber,
        string? carrierName,
        DateTimeOffset estimatedDelivery,
        decimal orderAmount,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Preparing to send order shipped email to {Email}", to);

        // Validate email address format
        if (string.IsNullOrWhiteSpace(to) || !to.Contains('@'))
        {
            this._logger.LogError("Invalid email address: {Email}", to);
            throw new InvalidEmailException($"Invalid email address: {to}");
        }

        // Simulate failure if configured (for testing error handling)
        if (this._options.SimulateFailure)
        {
            this._logger.LogWarning("Simulating email service failure (configured for testing)");
            throw new EmailServiceException("Simulated email service failure for testing");
        }

        // Simulate network delay
        if (this._options.DelayMilliseconds > 0)
        {
            await Task.Delay(this._options.DelayMilliseconds, cancellationToken);
        }

        // Log what would be sent in a real implementation
        var deliveryDateFormatted = estimatedDelivery.ToString("MMMM dd, yyyy");
        var carrier = string.IsNullOrWhiteSpace(carrierName) ? "the carrier" : carrierName;

        this._logger.LogInformation(
            "EMAIL SENT (simulated)\n" +
            "  To: {Email}\n" +
            "  Subject: Your Order #{OrderId} Has Shipped!\n" +
            "  Body Preview:\n" +
            "    Hi {CustomerName},\n" +
            "    \n" +
            "    Great news! Your order #{OrderId} (${OrderAmount:F2}) has shipped and is on its way.\n" +
            "    \n" +
            "    Tracking Number: {TrackingNumber}\n" +
            "    Carrier: {Carrier}\n" +
            "    Estimated Delivery: {DeliveryDate}\n" +
            "    \n" +
            "    You can track your package using the tracking number above.\n" +
            "    \n" +
            "    Thank you for your order!\n",
            to,
            orderId,
            customerName,
            orderId,
            orderAmount,
            trackingNumber,
            carrier,
            deliveryDateFormatted);

        this._logger.LogInformation("Order shipped email sent successfully to {Email}", to);
    }
}

/// <summary>
/// Configuration options for the mock email service.
/// </summary>
public class MockEmailServiceOptions
{
    /// <summary>
    /// Delay in milliseconds to simulate email sending latency.
    /// </summary>
    public int DelayMilliseconds { get; set; } = 150;

    /// <summary>
    /// If true, the service will throw an exception to simulate failure (for testing error handling).
    /// </summary>
    public bool SimulateFailure { get; set; }
}
