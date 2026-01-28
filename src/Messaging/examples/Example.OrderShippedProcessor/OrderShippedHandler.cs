using Microsoft.Extensions.Logging;

namespace Femur.Messaging.Example.OrderShippedProcessor;

/// <summary>
/// Handler for processing OrderShipped messages from the Service Bus queue.
/// This handler validates the message, sends a shipping notification email to the customer,
/// and demonstrates proper error handling patterns with Femur.Messaging.
/// </summary>
public class OrderShippedHandler : IMessageHandler<OrderShippedMessage>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderShippedHandler> _logger;

    public OrderShippedHandler(
        IEmailService emailService,
        ILogger<OrderShippedHandler> logger)
    {
        this._emailService = emailService;
        this._logger = logger;
    }

    public async Task HandleAsync(OrderShippedMessage message, CancellationToken cancellationToken)
    {
        this._logger.LogInformation(
            "Processing OrderShipped message for Order {OrderId}, Customer {CustomerId}",
            message.OrderId,
            message.CustomerId);

        // Validate message - throws DeadLetterException for invalid data
        this.ValidateMessage(message);

        // === EMAIL SENDING PHASE ===
        // Send the shipping notification email
        // Handle different exception types appropriately:
        // - InvalidEmailException: Dead-letter (permanent failure)
        // - EmailServiceException: Let bubble up for retry (transient failure)

        try
        {
            await this._emailService.SendOrderShippedEmailAsync(
                to: message.CustomerEmail,
                customerName: message.CustomerName,
                orderId: message.OrderId,
                trackingNumber: message.ShippingTrackingNumber,
                carrierName: message.CarrierName,
                estimatedDelivery: message.EstimatedDeliveryDate,
                orderAmount: message.OrderAmount,
                cancellationToken: cancellationToken);

            this._logger.LogInformation(
                "Successfully processed OrderShipped message for Order {OrderId}. Email sent to {Email}",
                message.OrderId,
                message.CustomerEmail);
        }
        catch (InvalidEmailException ex)
        {
            // Invalid email is a permanent failure - dead-letter the message
            this._logger.LogError(ex,
                "Invalid email address {Email} for Order {OrderId}. Dead-lettering message.",
                message.CustomerEmail,
                message.OrderId);

            throw new DeadLetterException(
                "InvalidEmailAddress",
                $"Email address '{message.CustomerEmail}' is invalid: {ex.Message}");
        }
        catch (EmailServiceException ex)
        {
            // Email service errors are typically transient (rate limits, network issues)
            // Let this exception bubble up so the message is abandoned and retried
            this._logger.LogWarning(ex,
                "Email service error while processing Order {OrderId}. Message will be retried.",
                message.OrderId);

            throw; // Rethrow to trigger retry via message abandonment
        }
    }

    /// <summary>
    /// Validates the OrderShipped message and throws DeadLetterException for any validation failures.
    /// These are data quality issues that won't be fixed by retrying.
    /// </summary>
    private void ValidateMessage(OrderShippedMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.CustomerEmail))
        {
            this._logger.LogError("OrderShipped message missing CustomerEmail for Order {OrderId}", message.OrderId);
            throw new DeadLetterException(
                "MissingCustomerEmail",
                $"CustomerEmail is required but was missing for Order {message.OrderId}");
        }

        if (string.IsNullOrWhiteSpace(message.CustomerName))
        {
            this._logger.LogError("OrderShipped message missing CustomerName for Order {OrderId}", message.OrderId);
            throw new DeadLetterException(
                "MissingCustomerName",
                $"CustomerName is required but was missing for Order {message.OrderId}");
        }

        if (string.IsNullOrWhiteSpace(message.ShippingTrackingNumber))
        {
            this._logger.LogError("OrderShipped message missing ShippingTrackingNumber for Order {OrderId}", message.OrderId);
            throw new DeadLetterException(
                "MissingTrackingNumber",
                $"ShippingTrackingNumber is required but was missing for Order {message.OrderId}");
        }

        if (message.EstimatedDeliveryDate == default)
        {
            this._logger.LogError("OrderShipped message has invalid EstimatedDeliveryDate for Order {OrderId}", message.OrderId);
            throw new DeadLetterException(
                "InvalidDeliveryDate",
                $"EstimatedDeliveryDate is required but was not set for Order {message.OrderId}");
        }

        this._logger.LogDebug(
            "Validation passed for Order {OrderId}. Tracking: {TrackingNumber}, Delivery: {DeliveryDate}",
            message.OrderId,
            message.ShippingTrackingNumber,
            message.EstimatedDeliveryDate);
    }
}
