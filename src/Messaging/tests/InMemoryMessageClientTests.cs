using Femur.Messaging;
using Femur.Messaging.Example;
using Femur.Messaging.InMemory;

namespace Femur.Messaging.Tests;

public class InMemoryMessageClientTests
{
    private static async Task<T?> FirstOrDefaultAsync<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            return item;
        }
        return default;
    }

    [Fact]
    public async Task ReceiveAsync_WithPublishedMessage_ReturnsMessage()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        var orderId = Guid.NewGuid();
        await queue.PublishAsync(new OrderMessage
        {
            OrderId = orderId,
            CustomerId = "cust-123",
            Amount = 99.99m,
            Items = [new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = 99.99m }]
        });

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var receivedMessage = await FirstOrDefaultAsync(client.ReceiveAsync(cts.Token), cts.Token);

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.True(receivedMessage.IsValid);
        Assert.Equal(orderId, receivedMessage.Body.OrderId);
        Assert.Equal("cust-123", receivedMessage.Body.CustomerId);
        Assert.Null(receivedMessage.DeserializationError);
    }

    [Fact]
    public async Task ReceiveBatchAsync_WithMultipleMessages_ReturnsAll()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        var messages = new[]
        {
            new OrderMessage { OrderId = Guid.NewGuid(), CustomerId = "cust-1", Amount = 10m, Items = [] },
            new OrderMessage { OrderId = Guid.NewGuid(), CustomerId = "cust-2", Amount = 20m, Items = [] },
            new OrderMessage { OrderId = Guid.NewGuid(), CustomerId = "cust-3", Amount = 30m, Items = [] }
        };

        foreach (var msg in messages)
        {
            await queue.PublishAsync(msg);
        }

        // Act
        var received = await client.ReceiveBatchAsync(maxMessages: 3, maxWaitTime: TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(3, received.Count);
        Assert.Equal("cust-1", received[0].Body.CustomerId);
        Assert.Equal("cust-2", received[1].Body.CustomerId);
        Assert.Equal("cust-3", received[2].Body.CustomerId);
    }

    [Fact]
    public async Task ReceiveBatchAsync_WithFewerMessagesThanMax_ReturnsAvailable()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        await queue.PublishAsync(new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-1",
            Amount = 10m,
            Items = []
        });

        // Act
        var received = await client.ReceiveBatchAsync(maxMessages: 10, maxWaitTime: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Single(received);
    }

    [Fact]
    public async Task CompleteAsync_RemovesMessageFromQueue()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        await queue.PublishAsync(new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-123",
            Amount = 99.99m,
            Items = []
        });

        var received = await client.ReceiveBatchAsync(1);
        var message = received[0];

        // Act
        await client.CompleteAsync(message);

        // Assert
        var completed = queue.GetCompletedMessages(OrderMessage.MessageName);
        Assert.Single(completed);
        Assert.Equal(InMemoryMessageState.Completed, completed[0].State);
    }

    [Fact]
    public async Task AbandonAsync_RequeuesMessage()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        var orderId = Guid.NewGuid();
        await queue.PublishAsync(new OrderMessage
        {
            OrderId = orderId,
            CustomerId = "cust-123",
            Amount = 99.99m,
            Items = []
        });

        var received = await client.ReceiveBatchAsync(1);
        var message = received[0];

        // Act
        await client.AbandonAsync(message);

        // Give it a moment to requeue
        await Task.Delay(50);

        // Assert - should be able to receive it again
        var receivedAgain = await client.ReceiveBatchAsync(1, TimeSpan.FromMilliseconds(100));
        Assert.Single(receivedAgain);
        Assert.Equal(orderId, receivedAgain[0].Body.OrderId);
    }

    [Fact]
    public async Task AbandonAsync_WithProperties_UpdatesMessageProperties()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        await queue.PublishAsync(new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-123",
            Amount = 99.99m,
            Items = []
        });

        var received = await client.ReceiveBatchAsync(1);
        var message = received[0];

        var properties = new Dictionary<string, object>
        {
            ["RetryCount"] = 1,
            ["LastError"] = "Temporary failure"
        };

        // Act
        await client.AbandonAsync(message, properties);
        await Task.Delay(50);

        // Assert
        var receivedAgain = await client.ReceiveBatchAsync(1, TimeSpan.FromMilliseconds(100));
        Assert.Single(receivedAgain);
        Assert.Equal(1, receivedAgain[0].Properties["RetryCount"]);
        Assert.Equal("Temporary failure", receivedAgain[0].Properties["LastError"]);
    }

    [Fact]
    public async Task DeadLetterAsync_MovesToDeadLetterQueue()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        await queue.PublishAsync(new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-123",
            Amount = -10m, // Invalid
            Items = []
        });

        var received = await client.ReceiveBatchAsync(1);
        var message = received[0];

        // Act
        await client.DeadLetterAsync(message, "InvalidAmount", "Amount must be positive");

        // Assert
        var deadLettered = queue.GetDeadLetterMessages(OrderMessage.MessageName);
        Assert.Single(deadLettered);
        Assert.Equal("InvalidAmount", deadLettered[0].DeadLetterReason);
        Assert.Equal("Amount must be positive", deadLettered[0].DeadLetterDescription);
        Assert.Equal(InMemoryMessageState.DeadLettered, deadLettered[0].State);
    }

    [Fact]
    public async Task DeadLetterAsync_WithProperties_AddsPropertiesToDeadLetteredMessage()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        await queue.PublishAsync(new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-123",
            Amount = 0m,
            Items = []
        });

        var received = await client.ReceiveBatchAsync(1);
        var message = received[0];

        var properties = new Dictionary<string, object>
        {
            ["FailureTime"] = DateTimeOffset.UtcNow,
            ["OriginalAmount"] = 0m
        };

        // Act
        await client.DeadLetterAsync(message, "EmptyOrder", null, properties);

        // Assert
        var deadLettered = queue.GetDeadLetterMessages(OrderMessage.MessageName);
        Assert.Single(deadLettered);
        Assert.Contains("FailureTime", deadLettered[0].Properties.Keys);
        Assert.Equal(0m, deadLettered[0].Properties["OriginalAmount"]);
    }

    [Fact]
    public async Task ReceiveAsync_WithInvalidJson_ReturnsMessageWithError()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        // Manually publish invalid data
        var channel = queue.GetOrCreateChannel(OrderMessage.MessageName);
        var invalidEnvelope = new InMemoryEnvelope
        {
            MessageId = "msg-invalid",
            MessageName = OrderMessage.MessageName,
            Body = System.Text.Encoding.UTF8.GetBytes("{ invalid json }")
        };
        await channel.Writer.WriteAsync(invalidEnvelope);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var receivedMessage = await FirstOrDefaultAsync(client.ReceiveAsync(cts.Token), cts.Token);

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.False(receivedMessage.IsValid);
        Assert.NotNull(receivedMessage.DeserializationError);
    }

    [Fact]
    public async Task ReceiveAsync_MessageProperties_AreAccessible()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        var correlationId = Guid.NewGuid().ToString();
        await queue.PublishAsync(
            new OrderMessage { OrderId = Guid.NewGuid(), CustomerId = "cust-123", Amount = 50m, Items = [] },
            correlationId: correlationId);

        // Act
        var received = await client.ReceiveBatchAsync(1);

        // Assert
        var message = received[0];
        Assert.NotNull(message.MessageId);
        Assert.Equal(correlationId, message.CorrelationId);
        Assert.Equal(1, message.DeliveryCount);
        Assert.True(message.EnqueuedTime <= DateTimeOffset.UtcNow);
        Assert.NotNull(message.Properties);
    }

    [Fact]
    public async Task DisposeAsync_CompletesSuccessfully()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var serializer = new JsonMessageSerializer();
        var client = new InMemoryTransport(queue).CreateClient<OrderMessage>(serializer);

        // Act & Assert
        await client.DisposeAsync(); // Should not throw
    }
}
