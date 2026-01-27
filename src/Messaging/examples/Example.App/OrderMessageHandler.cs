using Microsoft.Extensions.Logging;

namespace Femur.Messaging.Example;

public class OrderMessageHandler : IMessageHandler<OrderMessage>
{
    private readonly ILogger<OrderMessageHandler> _logger;

    public OrderMessageHandler(ILogger<OrderMessageHandler> logger)
    {
        this._logger = logger;
    }

    public async Task HandleAsync(OrderMessage message, CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Processing order {OrderId} for customer {CustomerId}",
            message.OrderId, message.CustomerId);

        // Validation - throw DeadLetterException for permanently invalid messages
        if (message.Amount <= 0)
        {
            throw new DeadLetterException("InvalidAmount", $"Amount was {message.Amount}");
        }

        if (message.Items.Count == 0)
        {
            throw new DeadLetterException("EmptyOrder", "Order must contain at least one item");
        }

        // Your business logic here
        await Task.Delay(100, cancellationToken);

        this._logger.LogInformation("Order {OrderId} processed", message.OrderId);
    }
}
