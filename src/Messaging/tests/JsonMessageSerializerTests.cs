using System.Text;
using Femur.Messaging;
using Femur.Messaging.Example;

namespace Femur.Messaging.Tests;

public class JsonMessageSerializerTests
{
    [Fact]
    public void Serialize_ValidMessage_ReturnsBytes()
    {
        // Arrange
        var serializer = new JsonMessageSerializer();
        var message = new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-123",
            Amount = 99.99m,
            Items = [new OrderItem { ProductId = "prod-1", Quantity = 2, UnitPrice = 49.995m }]
        };

        // Act
        var bytes = serializer.Serialize(message);

        // Assert
        Assert.True(bytes.Length > 0);
        var json = Encoding.UTF8.GetString(bytes.Span);
        Assert.Contains("cust-123", json);
        Assert.Contains("prod-1", json);
    }

    [Fact]
    public void Deserialize_ValidBytes_ReturnsMessage()
    {
        // Arrange
        var serializer = new JsonMessageSerializer();
        var orderId = Guid.NewGuid();
        var message = new OrderMessage
        {
            OrderId = orderId,
            CustomerId = "cust-456",
            Amount = 150.00m,
            Items = [
                new OrderItem { ProductId = "prod-1", Quantity = 1, UnitPrice = 100.00m },
                new OrderItem { ProductId = "prod-2", Quantity = 2, UnitPrice = 25.00m }
            ]
        };

        var bytes = serializer.Serialize(message);

        // Act
        var deserialized = serializer.Deserialize<OrderMessage>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(orderId, deserialized.OrderId);
        Assert.Equal("cust-456", deserialized.CustomerId);
        Assert.Equal(150.00m, deserialized.Amount);
        Assert.Equal(2, deserialized.Items.Count);
        Assert.Equal("prod-1", deserialized.Items[0].ProductId);
        Assert.Equal(1, deserialized.Items[0].Quantity);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsException()
    {
        // Arrange
        var serializer = new JsonMessageSerializer();
        var invalidJson = Encoding.UTF8.GetBytes("{ invalid json }");

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => serializer.Deserialize<OrderMessage>(invalidJson));
    }

    [Fact]
    public void Deserialize_NullResult_ThrowsException()
    {
        // Arrange
        var serializer = new JsonMessageSerializer();
        var nullJson = Encoding.UTF8.GetBytes("null");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => serializer.Deserialize<OrderMessage>(nullJson));
        Assert.Contains("null", ex.Message);
    }

    [Fact]
    public void RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var serializer = new JsonMessageSerializer();
        var orderId = Guid.NewGuid();
        var original = new OrderMessage
        {
            OrderId = orderId,
            CustomerId = "customer-789",
            Amount = 299.99m,
            Items = [
                new OrderItem { ProductId = "prod-a", Quantity = 3, UnitPrice = 99.99m }
            ]
        };

        // Act
        var bytes = serializer.Serialize(original);
        var roundTripped = serializer.Deserialize<OrderMessage>(bytes);

        // Assert
        Assert.Equal(original.OrderId, roundTripped.OrderId);
        Assert.Equal(original.CustomerId, roundTripped.CustomerId);
        Assert.Equal(original.Amount, roundTripped.Amount);
        Assert.Equal(original.Items.Count, roundTripped.Items.Count);
        Assert.Equal(original.Items[0].ProductId, roundTripped.Items[0].ProductId);
        Assert.Equal(original.Items[0].Quantity, roundTripped.Items[0].Quantity);
        Assert.Equal(original.Items[0].UnitPrice, roundTripped.Items[0].UnitPrice);
    }

    [Fact]
    public void Serialize_EmptyItemsList_HandlesCorrectly()
    {
        // Arrange
        var serializer = new JsonMessageSerializer();
        var message = new OrderMessage
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "cust-empty",
            Amount = 0m,
            Items = []
        };

        // Act
        var bytes = serializer.Serialize(message);
        var deserialized = serializer.Deserialize<OrderMessage>(bytes);

        // Assert
        Assert.NotNull(deserialized.Items);
        Assert.Empty(deserialized.Items);
    }
}
