// This file demonstrates how to use a custom serializer with the messaging framework.

using System.Text;
using System.Text.Json;
using Femur.Messaging;
using Femur.Messaging.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// ============================================================================
// EXAMPLE 1: Using Default JSON Serializer (implicit)
// ============================================================================
// When no serializer is specified, JsonMessageSerializer is used by default
builder.Services.AddFemurInMemoryTransport();
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>();
builder.Services.AddMessageClient<OrderMessage>();

// ============================================================================
// EXAMPLE 2: Custom JSON Options
// ============================================================================
// You can customize the JSON serialization by providing your own options
var customJsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
};

builder.Services.AddFemurInMemoryTransport();
builder.Services.AddMessageHandler<CustomerMessage, CustomerMessageHandler>(
    new JsonMessageSerializer(customJsonOptions));
builder.Services.AddMessageClient<CustomerMessage>(
    new JsonMessageSerializer(customJsonOptions));

// ============================================================================
// EXAMPLE 3: Custom XML Serializer
// ============================================================================
// You can implement any custom serializer for XML, Protobuf, MessagePack, etc.
builder.Services.AddFemurInMemoryTransport();
builder.Services.AddMessageHandler<ProductMessage, ProductMessageHandler>(
    new XmlMessageSerializer());
builder.Services.AddMessageClient<ProductMessage>(
    new XmlMessageSerializer());

var app = builder.Build();

// Send a few messages to demonstrate the different serializers
var orderClient = app.Services.GetRequiredService<IMessageClient<OrderMessage>>();
await orderClient.SendAsync(new OrderMessage { OrderId = "ORDER-001", Amount = 99.99m });

var customerClient = app.Services.GetRequiredService<IMessageClient<CustomerMessage>>();
await customerClient.SendAsync(new CustomerMessage { CustomerId = "CUST-001", Name = "John Doe" });

var productClient = app.Services.GetRequiredService<IMessageClient<ProductMessage>>();
await productClient.SendAsync(new ProductMessage { ProductId = "PROD-001", Name = "Widget" });

await app.RunAsync();

// ============================================================================
// Message Types
// ============================================================================

public record OrderMessage
{
    public required string OrderId { get; init; }
    public decimal Amount { get; init; }
}

public record CustomerMessage
{
    public required string CustomerId { get; init; }
    public required string Name { get; init; }
}

public record ProductMessage
{
    public required string ProductId { get; init; }
    public required string Name { get; init; }
}

// ============================================================================
// Message Handlers
// ============================================================================

public class OrderMessageHandler : IMessageHandler<OrderMessage>
{
    public async Task HandleAsync(OrderMessage message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[JSON] Processing order: {message.OrderId} for ${message.Amount}");
        await Task.CompletedTask;
    }
}

public class CustomerMessageHandler : IMessageHandler<CustomerMessage>
{
    public async Task HandleAsync(CustomerMessage message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Custom JSON] Processing customer: {message.CustomerId} - {message.Name}");
        await Task.CompletedTask;
    }
}

public class ProductMessageHandler : IMessageHandler<ProductMessage>
{
    public async Task HandleAsync(ProductMessage message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[XML] Processing product: {message.ProductId} - {message.Name}");
        await Task.CompletedTask;
    }
}

// ============================================================================
// Custom XML Serializer Implementation
// ============================================================================

/// <summary>
/// Example custom serializer using XML instead of JSON.
/// In a real application, you might use System.Xml.Serialization.XmlSerializer
/// or a more sophisticated XML library.
/// </summary>
public class XmlMessageSerializer : IMessageSerializer
{
    public ReadOnlyMemory<byte> Serialize<T>(T message) where T : class
    {
        // Simple XML representation (in production, use proper XML serializer)
        var xml = $"<{typeof(T).Name}>";
        
        foreach (var prop in typeof(T).GetProperties())
        {
            var value = prop.GetValue(message);
            xml += $"<{prop.Name}>{value}</{prop.Name}>";
        }
        
        xml += $"</{typeof(T).Name}>";
        
        return Encoding.UTF8.GetBytes(xml);
    }

    public T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class
    {
        // Simple XML deserialization (in production, use proper XML deserializer)
        var xml = Encoding.UTF8.GetString(data.Span);
        
        // For this example, we'll just use JSON as a fallback
        // In a real implementation, you'd parse the XML properly
        throw new NotImplementedException(
            "XML deserialization requires proper XML parser. " +
            "This is a demonstration of the serializer interface pattern.");
    }
}
