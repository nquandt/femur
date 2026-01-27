using Microsoft.Extensions.Logging;

namespace Femur.Messaging.Example.MultiTransport;

public class OrderMessageHandler : IMessageHandler<OrderMessage>
{
    private readonly ILogger<OrderMessageHandler> _logger;

    public OrderMessageHandler(ILogger<OrderMessageHandler> logger)
    {
        this._logger = logger;
    }

    public Task HandleAsync(OrderMessage message, CancellationToken cancellationToken)
    {
        this._logger.LogInformation(
            "Processing order {OrderId} for {Customer} - Amount: {Amount:C}",
            message.OrderId,
            message.CustomerName,
            message.Amount);

        return Task.CompletedTask;
    }
}
