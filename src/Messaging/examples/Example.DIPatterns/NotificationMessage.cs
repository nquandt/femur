namespace Femur.Messaging.Example.DIPatterns;

public class NotificationMessage : IMessage
{
    public static string MessageName => "notifications";
    public string To { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}
