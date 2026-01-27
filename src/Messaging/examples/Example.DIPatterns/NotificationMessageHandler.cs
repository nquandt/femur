using Microsoft.Extensions.Logging;

namespace Femur.Messaging.Example.DIPatterns;

public class NotificationMessageHandler : IMessageHandler<NotificationMessage>
{
    private readonly ILogger<NotificationMessageHandler> _logger;

    public NotificationMessageHandler(ILogger<NotificationMessageHandler> logger)
    {
        this._logger = logger;
    }

    public Task HandleAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        this._logger.LogInformation(
            "Sending notification to {To}: {Subject}",
            message.To,
            message.Subject);

        return Task.CompletedTask;
    }
}
