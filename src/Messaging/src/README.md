# Femur.Messaging

A simple, opinionated message processing framework for .NET. Just implement `IMessageHandler<TMessage>` and go.

## Overview

Femur.Messaging provides a clean abstraction for processing messages from any transport (Azure Service Bus, RabbitMQ, in-memory queues, etc.). It handles the boilerplate of message processing, dead-letter handling, and lifecycle management through hosted services.

## Installation

```bash
dotnet add package Femur.Messaging
```

## Basic Usage

### 1. Define Your Message

```csharp
public class OrderMessage : IMessage
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
```

### 2. Implement a Message Handler

```csharp
public class OrderMessageHandler : IMessageHandler<OrderMessage>
{
    private readonly ILogger<OrderMessageHandler> _logger;

    public OrderMessageHandler(ILogger<OrderMessageHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(OrderMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing order {OrderId} for ${Amount}", 
            message.OrderId, message.Amount);
        
        // Your business logic here
        await Task.CompletedTask;
    }
}
```

### 3. Register Services and Configure

**Recommended: Using Fluent API with DI Resolution**

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Configure Service Bus settings
builder.Services.Configure<ServiceBusConfig>(
    builder.Configuration.GetSection("ServiceBus"));

// Register handler with message processor using fluent API
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>()
    .Configure(options =>
    {
        // Configure message processor behavior
        options.MaxDeliveryCount = 5;
    });

// Add transport - resolve configuration from DI at runtime
builder.Services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("ServiceBus")!,
    configure: options => options.QueueName = builder.Configuration["ServiceBus:QueueName"]!);

var host = builder.Build();
await host.RunAsync();
```

**Alternative: Direct Configuration**

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Register handler
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>()
    .Configure(options =>
    {
        // Configure message processor behavior
        options.MaxDeliveryCount = 5;
    });

// Add transport with direct configuration
builder.Services.AddFemurInMemory(); // For testing

var host = builder.Build();
await host.RunAsync();
```

## Serialization

The messaging framework is serialization-agnostic. You can use JSON (default), XML, Protobuf, MessagePack, or any custom format.

### Default JSON Serializer

By default, messages are serialized using `JsonMessageSerializer`:

```csharp
// Uses JSON serialization automatically
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>();
builder.Services.AddMessageClient<OrderMessage>();
```

### Custom JSON Options

Customize JSON serialization behavior:

```csharp
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>(
    new JsonMessageSerializer(jsonOptions));

builder.Services.AddMessageClient<OrderMessage>(
    new JsonMessageSerializer(jsonOptions));
```

### Custom Serializer

Implement `IMessageSerializer` for custom formats:

```csharp
public class ProtobufSerializer : IMessageSerializer
{
    public ReadOnlyMemory<byte> Serialize<T>(T message) where T : class
    {
        using var stream = new MemoryStream();
        ProtoBuf.Serializer.Serialize(stream, message);
        return stream.ToArray();
    }

    public T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class
    {
        using var stream = new MemoryStream(data.ToArray());
        return ProtoBuf.Serializer.Deserialize<T>(stream);
    }
}

// Use custom serializer
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>(
    new ProtobufSerializer());
builder.Services.AddMessageClient<OrderMessage>(
    new ProtobufSerializer());
```

**Important:** The same serializer must be used for both handler and client to ensure proper message encoding/decoding.

See `examples/CustomSerializer` for a complete example with XML serialization.

## Transports

Femur.Messaging requires a transport implementation. Available transports:

- **Femur.Messaging.InMemory** - In-memory queue for testing
- **Femur.Messaging.ServiceBus** - Azure Service Bus transport

## Key Interfaces

### IMessage
Marker interface for messages. All messages must implement this interface.

### IMessageHandler<TMessage>
Implement this interface to handle messages of type `TMessage`.

```csharp
public interface IMessageHandler<in TMessage> where TMessage : IMessage
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}
```

**Important:** Message handlers are registered as **scoped services**. This means:
- A new handler instance is created for each message
- You can inject scoped dependencies like `DbContext`, `IHttpContextAccessor`, etc.
- Each message gets its own DI scope, perfect for transaction boundaries
- Scoped services are disposed after message processing completes

**Example with scoped DbContext:**
```csharp
public class OrderMessageHandler : IMessageHandler<OrderMessage>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OrderMessageHandler> _logger;

    public OrderMessageHandler(ApplicationDbContext db, ILogger<OrderMessageHandler> logger)
    {
        this._db = db;  // Scoped - new instance per message
        this._logger = logger;
    }

    public async Task HandleAsync(OrderMessage message, CancellationToken cancellationToken)
    {
        // DbContext is scoped to this message - perfect for transactions
        var order = new Order { Id = message.OrderId, Amount = message.Amount };
        this._db.Orders.Add(order);
        await this._db.SaveChangesAsync(cancellationToken);
    }
}
```

### IMessageSerializer
Abstraction for message serialization. Implement this to support custom formats.

```csharp
public interface IMessageSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T message) where T : class;
    T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class;
}
```

### IMessagingTransport
Transport abstraction that connects to message brokers and provides message receivers.

## Dead Letter Handling

To send a message to the dead-letter queue, throw a `DeadLetterException`:

```csharp
public async Task HandleAsync(OrderMessage message, CancellationToken cancellationToken)
{
    if (!IsValid(message))
    {
        throw new DeadLetterException("Invalid message format", "INVALID_FORMAT");
    }
    
    // Process message
}
```

## Configuration

Configure message processing options:

```csharp
builder.Services.Configure<MessageProcessorOptions>(options =>
{
    options.MaxConcurrentMessages = 10;
    options.MaxRetries = 3;
    options.RetryDelay = TimeSpan.FromSeconds(5);
});
```

## Advanced: Multiple Transports

You can use multiple transports in the same application by registering them with keys:

```csharp
// Register multiple transports with different keys
builder.Services.AddFemurInMemory(transportKey: "local");
builder.Services.AddFemurServiceBus(connectionString, transportKey: "azure");

// Route different message types to different transports
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>()
    .UseTransport("local");  // Orders use in-memory transport

builder.Services.AddMessageHandler<PaymentMessage, PaymentMessageHandler>()
    .UseTransport("azure");  // Payments use Service Bus
```

This allows you to:
- Process different message types from different queues
- Use different transports for different purposes (e.g., local for testing, Service Bus for production)
- Scale different message handlers independently

**Note:** If you don't call `UseTransport()`, the handler will use the default (non-keyed) transport.

## Distributed Tracing & Observability

The framework automatically creates **Activities** for distributed tracing using `System.Diagnostics.ActivitySource`. Each message gets:

- **Activity Name:** `ProcessMessage`
- **Activity Kind:** `Consumer`
- **Tags:**
  - `messaging.message_id` - Unique message identifier
  - `messaging.destination` - Queue/topic name
  - `messaging.delivery_count` - How many times message was delivered
  - `messaging.correlation_id` - Correlation ID if present

This integrates seamlessly with:
- **OpenTelemetry** - Export traces to Jaeger, Zipkin, Application Insights
- **Application Insights** - Azure monitoring and diagnostics
- **Custom telemetry** - Any ActivityListener-based solution

**Example with OpenTelemetry:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Femur.Messaging")  // ← Subscribe to message processing traces
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
```

Activities automatically capture exceptions and set status codes:
- `Ok` - Message processed successfully
- `Error` - Processing failed (includes exception details)

**Custom Activity Tags:**
```csharp
public async Task HandleAsync(OrderMessage message, CancellationToken cancellationToken)
{
    var activity = Activity.Current;
    activity?.SetTag("order.customer_id", message.CustomerId);
    activity?.SetTag("order.amount", message.Amount);
    
    // Your processing logic
}
```

## Manual Message Consumption

For scenarios where you want to control message consumption yourself instead of automatic background processing, use `IMessageConsumer<T>`:

```csharp
// Register consumer (no automatic processing)
builder.Services.AddMessageConsumer<OrderMessage>();

// Inject and use manually
public class MyService
{
    private readonly IMessageConsumer<OrderMessage> _consumer;
    
    public async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        // Option 1: Consume as async stream
        await foreach (var message in _consumer.ConsumeAsync(cancellationToken))
        {
            try
            {
                // Process the message
                await ProcessOrderAsync(message.Message!);
                
                // Complete when done
                await message.CompleteAsync();
            }
            catch (Exception ex)
            {
                // Abandon for retry or dead-letter
                await message.AbandonAsync();
            }
        }
        
        // Option 2: Consume in batches
        var batch = await _consumer.ConsumeBatchAsync(
            maxMessages: 10,
            maxWaitTime: TimeSpan.FromSeconds(30));
        
        foreach (var message in batch)
        {
            // Process batch...
        }
    }
}
```

**When to use manual consumption:**
- You need explicit control over when messages are processed
- Batch processing scenarios
- Console applications without hosting
- Custom processing logic outside the standard handler pattern
- Testing scenarios where you want to control message flow

**When to use automatic processing (AddMessageHandler):**
- Standard background message processing
- You want the framework to handle retries and error handling
- Hosted service scenarios
- Most production use cases

## How It Works

1. `MessageProcessorHostedService` starts when the host starts
2. It creates a `MessageProcessor<TMessage>` for each message type
3. The processor gets an `IMessageReceiver` from the configured `IMessagingTransport`
4. Messages are received, deserialized, and passed to registered handlers
5. Successfully processed messages are completed; failed messages are retried or dead-lettered

## License

MIT
