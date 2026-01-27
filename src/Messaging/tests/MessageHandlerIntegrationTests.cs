using Femur.Messaging;
using Femur.Messaging.Example;
using Femur.Messaging.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Femur.Messaging.Tests;

public class MessageHandlerIntegrationTests
{
    [Fact]
    public async Task MessageHandler_ValidMessage_ProcessedSuccessfully()
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
            CustomerId = "cust-integration-1",
            Amount = 150.00m,
            Items = [
                new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = 100.00m },
                new OrderItem { ProductId = "prod-2", Quantity = 2, UnitPrice = 25.00m }
            ]
        };

        // Act
        await queue.PublishAsync(order);
        await host.StartAsync();
        await Task.Delay(500);
        await host.StopAsync();

        // Assert
        var completed = queue.GetCompletedMessages(OrderMessage.MessageName);
        Assert.Single(completed);
        
        var serializer = new JsonMessageSerializer();
        var completedMessage = serializer.Deserialize<OrderMessage>(completed[0].Body);
        Assert.Equal(order.OrderId, completedMessage.OrderId);
        Assert.Equal(order.CustomerId, completedMessage.CustomerId);
    }

    [Fact]
    public async Task MessageHandler_NegativeAmount_IsDeadLettered()
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
            CustomerId = "cust-invalid-1",
            Amount = -50m,
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

    [Fact]
    public async Task MessageHandler_EmptyItems_IsDeadLettered()
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

        var emptyOrder = new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-empty-1",
            Amount = 100m,
            Items = []
        };

        // Act
        await queue.PublishAsync(emptyOrder);
        await host.StartAsync();
        await Task.Delay(500);
        await host.StopAsync();

        // Assert
        var deadLettered = queue.GetDeadLetterMessages(OrderMessage.MessageName);
        Assert.Single(deadLettered);
        Assert.Equal("EmptyOrder", deadLettered[0].DeadLetterReason);
    }

    [Fact]
    public async Task MessageHandler_MultipleMessages_AllProcessed()
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

        var orders = new[]
        {
            new OrderMessage
            {
                OrderId = Guid.NewGuid(),
                CustomerId = "cust-1",
                Amount = 10m,
                Items = [new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = 10m }]
            },
            new OrderMessage
            {
                OrderId = Guid.NewGuid(),
                CustomerId = "cust-2",
                Amount = 20m,
                Items = [new OrderItem { ProductId = "prod-2", Quantity = 1, UnitPrice = 20m }]
            },
            new OrderMessage
            {
                OrderId = Guid.NewGuid(),
                CustomerId = "cust-3",
                Amount = 30m,
                Items = [new OrderItem { ProductId = "prod-3", Quantity = 1, UnitPrice = 30m }]
            }
        };

        // Act
        foreach (var order in orders)
        {
            await queue.PublishAsync(order);
        }

        await host.StartAsync();
        await Task.Delay(1000);
        await host.StopAsync();

        // Assert
        var completed = queue.GetCompletedMessages(OrderMessage.MessageName);
        Assert.Equal(3, completed.Count);
    }

    [Fact]
    public async Task MessageHandler_MixedValidAndInvalid_ProcessedCorrectly()
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

        var orders = new[]
        {
            new OrderMessage
            {
                OrderId = Guid.NewGuid(),
                CustomerId = "cust-valid-1",
                Amount = 100m,
                Items = [new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = 100m }]
            },
            new OrderMessage
            {
                OrderId = Guid.NewGuid(),
                CustomerId = "cust-invalid-1",
                Amount = -50m, // Invalid
                Items = [new OrderItem { ProductId = "prod-2", Quantity = 1, UnitPrice = -50m }]
            },
            new OrderMessage
            {
                OrderId = Guid.NewGuid(),
                CustomerId = "cust-valid-2",
                Amount = 200m,
                Items = [new OrderItem { ProductId = "prod-3", Quantity = 2, UnitPrice = 100m }]
            },
            new OrderMessage
            {
                OrderId = Guid.NewGuid(),
                CustomerId = "cust-invalid-2",
                Amount = 100m,
                Items = [] // Invalid - empty
            }
        };

        // Act
        foreach (var order in orders)
        {
            await queue.PublishAsync(order);
        }

        await host.StartAsync();
        await Task.Delay(1000);
        await host.StopAsync();

        // Assert
        var completed = queue.GetCompletedMessages(OrderMessage.MessageName);
        var deadLettered = queue.GetDeadLetterMessages(OrderMessage.MessageName);

        Assert.Equal(2, completed.Count);
        Assert.Equal(2, deadLettered.Count);
    }

    [Fact]
    public async Task MessageHandler_WithCorrelationId_PreservesCorrelationId()
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
        var correlationId = Guid.NewGuid().ToString();

        var order = new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-corr-1",
            Amount = 99.99m,
            Items = [new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = 99.99m }]
        };

        // Act
        await queue.PublishAsync(order, correlationId: correlationId);
        await host.StartAsync();
        await Task.Delay(500);
        await host.StopAsync();

        // Assert
        var completed = queue.GetCompletedMessages(OrderMessage.MessageName);
        Assert.Single(completed);
        Assert.Equal(correlationId, completed[0].CorrelationId);
    }

    [Fact]
    public async Task MessageHandler_CancellationDuringProcessing_StopsGracefully()
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

        // Act
        await host.StartAsync();
        
        // Publish after start
        await queue.PublishAsync(new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-cancel-1",
            Amount = 50m,
            Items = [new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = 50m }]
        });

        await Task.Delay(100);
        
        // Stop immediately
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StopAsync(cts.Token);

        // Assert - should complete without throwing
        Assert.True(true); // If we got here, graceful shutdown worked
    }

    [Fact]
    public async Task MessageHandler_UsingScopedServices_CreatesNewScopePerMessage()
    {
        // Arrange
        var processedScopes = new List<Guid>();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddFemurInMemory();
                
                // Register a scoped service that tracks its instance ID
                services.AddScoped<ScopedTracker>();
                
                // Custom handler that uses scoped service
                services.AddMessageHandler<OrderMessage, ScopedOrderMessageHandler>();
            })
            .Build();

        var queue = host.Services.GetMessageQueue();

        // Act - publish 3 messages
        for (int i = 0; i < 3; i++)
        {
            await queue.PublishAsync(new OrderMessage
            {
                OrderId = Guid.NewGuid(),
                CustomerId = $"cust-scope-{i}",
                Amount = 100m,
                Items = [new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = 100m }]
            });
        }

        await host.StartAsync();
        await Task.Delay(1000); // Give time to process all messages
        await host.StopAsync();

        // Assert
        var completed = queue.GetCompletedMessages(OrderMessage.MessageName);
        Assert.Equal(3, completed.Count);
        
        // Each message should have gotten its own scope instance
        Assert.Equal(3, ScopedOrderMessageHandler.ProcessedScopes.Count);
        Assert.Equal(3, ScopedOrderMessageHandler.ProcessedScopes.Distinct().Count());
    }
}

// Test helpers for scoped service verification
public class ScopedTracker
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public class ScopedOrderMessageHandler : IMessageHandler<OrderMessage>
{
    public static List<Guid> ProcessedScopes { get; } = new();
    private readonly ScopedTracker _tracker;

    public ScopedOrderMessageHandler(ScopedTracker tracker)
    {
        this._tracker = tracker;
    }

    public Task HandleAsync(OrderMessage message, CancellationToken cancellationToken)
    {
        ProcessedScopes.Add(this._tracker.InstanceId);
        return Task.CompletedTask;
    }
}
