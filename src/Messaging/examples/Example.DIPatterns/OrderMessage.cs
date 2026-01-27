namespace Femur.Messaging.Example.DIPatterns;

public class OrderMessage : IMessage
{
    public static string MessageName => "orders";
    public int OrderId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}
