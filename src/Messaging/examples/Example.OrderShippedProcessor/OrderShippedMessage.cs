namespace Femur.Messaging.Example.OrderShippedProcessor;

/// <summary>
/// Message representing an order that has been shipped and is ready for customer notification.
/// This message is published when an order leaves the warehouse and tracking information becomes available.
/// </summary>
public class OrderShippedMessage : IMessage
{
    /// <summary>
    /// The Service Bus queue name where these messages are published.
    /// </summary>
    public static string MessageName => "order-shipped";

    /// <summary>
    /// Unique identifier for the order.
    /// </summary>
    public required string OrderId { get; init; }

    /// <summary>
    /// Customer's unique identifier.
    /// </summary>
    public required string CustomerId { get; init; }

    /// <summary>
    /// Customer's email address where the shipping notification will be sent.
    /// </summary>
    public required string CustomerEmail { get; init; }

    /// <summary>
    /// Customer's full name for email personalization.
    /// </summary>
    public required string CustomerName { get; init; }

    /// <summary>
    /// Date when the order was originally placed.
    /// </summary>
    public DateTimeOffset OrderDate { get; init; }

    /// <summary>
    /// Tracking number from the shipping carrier (e.g., FedEx, UPS, USPS).
    /// </summary>
    public required string ShippingTrackingNumber { get; init; }

    /// <summary>
    /// Estimated delivery date provided by the shipping carrier.
    /// </summary>
    public DateTimeOffset EstimatedDeliveryDate { get; init; }

    /// <summary>
    /// Shipping carrier name (e.g., "FedEx", "UPS", "USPS").
    /// </summary>
    public string? CarrierName { get; init; }

    /// <summary>
    /// Total order amount for reference in the email.
    /// </summary>
    public decimal OrderAmount { get; init; }
}
