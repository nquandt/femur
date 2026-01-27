using Femur.Messaging;
using Femur.Messaging.Example;
using Femur.Messaging.InMemory;

namespace Femur.Messaging.Tests;

public class InMemoryMessageQueueTests
{
    [Fact]
    public async Task PublishAsync_AddsMessageToQueue()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var message = new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-123",
            Amount = 99.99m,
            Items = [new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = 99.99m }]
        };

        // Act
        await queue.PublishAsync(message);

        // Assert - message should be in pending state
        var channel = queue.GetOrCreateChannel(OrderMessage.MessageName);
        Assert.True(channel.Reader.TryPeek(out var envelope));
        Assert.Equal(OrderMessage.MessageName, envelope.MessageName);
    }

    [Fact]
    public async Task PublishAsync_WithCorrelationId_StoresCorrelationId()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var correlationId = Guid.NewGuid().ToString();
        var message = new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-456",
            Amount = 50.00m,
            Items = []
        };

        // Act
        await queue.PublishAsync(message, correlationId: correlationId);

        // Assert
        var channel = queue.GetOrCreateChannel(OrderMessage.MessageName);
        Assert.True(channel.Reader.TryPeek(out var envelope));
        Assert.Equal(correlationId, envelope.CorrelationId);
    }

    [Fact]
    public async Task PublishAsync_MultipleMessages_MaintainsOrder()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var messages = new[]
        {
            new OrderMessage { OrderId = Guid.NewGuid(), CustomerId = "cust-1", Amount = 10m, Items = [] },
            new OrderMessage { OrderId = Guid.NewGuid(), CustomerId = "cust-2", Amount = 20m, Items = [] },
            new OrderMessage { OrderId = Guid.NewGuid(), CustomerId = "cust-3", Amount = 30m, Items = [] }
        };

        // Act
        foreach (var msg in messages)
        {
            await queue.PublishAsync(msg);
        }

        // Assert
        var channel = queue.GetOrCreateChannel(OrderMessage.MessageName);
        var serializer = new JsonMessageSerializer();
        
        for (int i = 0; i < messages.Length; i++)
        {
            Assert.True(channel.Reader.TryRead(out var envelope));
            var deserialized = serializer.Deserialize<OrderMessage>(envelope.Body);
            Assert.Equal(messages[i].CustomerId, deserialized.CustomerId);
        }
    }

    [Fact]
    public void Complete_MovesMessageToCompletedList()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var envelope = new InMemoryEnvelope
        {
            MessageId = "msg-1",
            MessageName = "test-queue",
            Body = new byte[] { 1, 2, 3 }
        };

        // Act
        queue.Complete(envelope);

        // Assert
        var completed = queue.GetCompletedMessages("test-queue");
        Assert.Single(completed);
        Assert.Equal("msg-1", completed[0].MessageId);
        Assert.Equal(InMemoryMessageState.Completed, completed[0].State);
    }

    [Fact]
    public void DeadLetter_MovesMessageToDeadLetterQueue()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var envelope = new InMemoryEnvelope
        {
            MessageId = "msg-2",
            MessageName = "test-queue",
            Body = new byte[] { 4, 5, 6 }
        };

        // Act
        queue.DeadLetter(envelope, "InvalidData", "The data was malformed");

        // Assert
        var deadLettered = queue.GetDeadLetterMessages("test-queue");
        Assert.Single(deadLettered);
        Assert.Equal("msg-2", deadLettered[0].MessageId);
        Assert.Equal(InMemoryMessageState.DeadLettered, deadLettered[0].State);
        Assert.Equal("InvalidData", deadLettered[0].DeadLetterReason);
        Assert.Equal("The data was malformed", deadLettered[0].DeadLetterDescription);
    }

    [Fact]
    public void GetCompletedMessages_EmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();

        // Act
        var completed = queue.GetCompletedMessages("non-existent-queue");

        // Assert
        Assert.Empty(completed);
    }

    [Fact]
    public void GetDeadLetterMessages_EmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();

        // Act
        var deadLettered = queue.GetDeadLetterMessages("non-existent-queue");

        // Assert
        Assert.Empty(deadLettered);
    }

    [Fact]
    public async Task Clear_RemovesAllMessages()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        await queue.PublishAsync(new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-1",
            Amount = 10m,
            Items = []
        });

        var envelope = new InMemoryEnvelope
        {
            MessageId = "msg-1",
            MessageName = "test",
            Body = new byte[] { 1 }
        };
        queue.Complete(envelope);
        queue.DeadLetter(envelope, "reason", null);

        // Act
        queue.Clear();

        // Assert
        var completed = queue.GetCompletedMessages(OrderMessage.MessageName);
        var deadLettered = queue.GetDeadLetterMessages("test");
        Assert.Empty(completed);
        Assert.Empty(deadLettered);
    }

    [Fact]
    public async Task PublishAsync_GeneratesUniqueMessageIds()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();
        var messageIds = new HashSet<string>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            await queue.PublishAsync(new OrderMessage
            {
                OrderId = Guid.NewGuid(),
                CustomerId = $"cust-{i}",
                Amount = i,
                Items = []
            });
        }

        var channel = queue.GetOrCreateChannel(OrderMessage.MessageName);
        while (channel.Reader.TryRead(out var envelope))
        {
            messageIds.Add(envelope.MessageId);
        }

        // Assert
        Assert.Equal(100, messageIds.Count); // All unique
    }

    [Fact]
    public void GetOrCreateChannel_SameMessageName_ReturnsSameChannel()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();

        // Act
        var channel1 = queue.GetOrCreateChannel("orders");
        var channel2 = queue.GetOrCreateChannel("orders");

        // Assert
        Assert.Same(channel1, channel2);
    }

    [Fact]
    public void GetOrCreateChannel_DifferentMessageNames_ReturnsDifferentChannels()
    {
        // Arrange
        var queue = new InMemoryMessageQueue();

        // Act
        var channel1 = queue.GetOrCreateChannel("orders");
        var channel2 = queue.GetOrCreateChannel("notifications");

        // Assert
        Assert.NotSame(channel1, channel2);
    }
}
