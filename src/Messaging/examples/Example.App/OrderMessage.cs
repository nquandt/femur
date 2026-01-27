namespace Femur.Messaging.Example;

public class OrderMessage : IMessage
{
    public static string MessageName => "orders";

    public Guid OrderId { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public IReadOnlyList<OrderItem> Items { get; init; } = [];
}

public class OrderItem
{
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
