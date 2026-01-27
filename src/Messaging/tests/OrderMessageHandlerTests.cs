using Femur.Messaging;
using Femur.Messaging.Example;
using Femur.Messaging.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Example.Tests;

public class OrderMessageHandlerTests
{
    [Fact]
    public async Task ValidOrder_IsCompleted()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddFemurInMemory();
                services.AddMessageHandler<OrderMessage, OrderMessageHandler>();
            })
            .Build();

        var queue = host.Services.GetMessageQueue();

        var order = new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-123",
            Amount = 99.99m,
            Items = [new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = 99.99m }]
        };

        // Act
        await queue.PublishAsync(order);
        await host.StartAsync();
        await Task.Delay(500); // Give it time to process
        await host.StopAsync();

        // Assert
        var completed = queue.GetCompletedMessages(OrderMessage.MessageName);
        Assert.Single(completed);
        var serializer = new JsonMessageSerializer();
        var completedMessage = serializer.Deserialize<OrderMessage>(completed[0].Body);
        Assert.Equal(order.OrderId, completedMessage.OrderId);
    }

    [Fact]
    public async Task InvalidOrder_IsDeadLettered()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddFemurInMemory();
                services.AddMessageHandler<OrderMessage, OrderMessageHandler>();
            })
            .Build();

        var queue = host.Services.GetMessageQueue();

        var invalidOrder = new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-123",
            Amount = -50m, // Invalid!
            Items = [new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = -50m }]
        };

        // Act
        await queue.PublishAsync(invalidOrder);
        await host.StartAsync();
        await Task.Delay(500);
        await host.StopAsync();

        // Assert
        var deadLettered = queue.GetDeadLetterMessages(OrderMessage.MessageName);
        Assert.Single(deadLettered);
        Assert.Equal("InvalidAmount", deadLettered[0].DeadLetterReason);
    }
}
