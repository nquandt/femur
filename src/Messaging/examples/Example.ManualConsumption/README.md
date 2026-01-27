# Manual Message Consumption

This example demonstrates **manual message consumption** (pull-based) as an alternative to **automatic message processing** (push-based).

## Two Approaches

### Automatic Processing (Push-Based) - Default Pattern

```csharp
// 1. Register handler
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>();

// 2. Framework automatically:
//    - Starts background processing
//    - Calls your handler for each message
//    - Handles retries and dead-lettering
```

**Use when:**
- Standard background message processing
- You want the framework to handle retries and error handling
- Hosted service scenarios (most production cases)

### Manual Consumption (Pull-Based) - This Example

```csharp
// 1. Register consumer
builder.Services.AddMessageConsumer<OrderMessage>();

// 2. YOU control when to pull messages:
var consumer = serviceProvider.GetRequiredService<IMessageConsumer<OrderMessage>>();

// Stream messages
await foreach (var msg in consumer.ConsumeAsync(cancellationToken))
{
    await ProcessAsync(msg.Message!);
    await msg.CompleteAsync();
}

// OR pull batches
var batch = await consumer.ConsumeBatchAsync(maxMessages: 10);
```

**Use when:**
- You need explicit control over message consumption timing
- Batch processing scenarios
- Console applications without hosting infrastructure
- Custom processing logic outside standard handler pattern
- Testing scenarios where you want to control message flow

## Key Differences

| Feature | Automatic (AddMessageHandler) | Manual (AddMessageConsumer) |
|---------|------------------------------|----------------------------|
| Background Processing | ✅ Auto-starts with host | ❌ You control when |
| Handler Required | ✅ Must implement IMessageHandler | ❌ Process inline |
| Retry Logic | ✅ Built-in | ❌ You implement |
| Batching | ❌ One at a time | ✅ Pull batches |
| Settlement | ✅ Automatic | ❌ You call Complete/Abandon |
| Use Case | Production services | Batch jobs, testing, custom flows |

## Registration Comparison

**Automatic:**
```csharp
builder.Services.AddFemurInMemory();
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>()
    .Configure(options => options.MaxDeliveryCount = 5);
```

**Manual:**
```csharp
builder.Services.AddFemurInMemory();
builder.Services.AddMessageConsumer<OrderMessage>();
// No handler needed - you process inline
```

## Message Settlement

With manual consumption, YOU are responsible for settling messages:

```csharp
var message = ...; // from ConsumeAsync() or ConsumeBatchAsync()

// Success
await message.CompleteAsync();

// Retry
await message.AbandonAsync();

// Failed permanently
await message.DeadLetterAsync("Reason", "Description");
```

## See Also

- [Program.cs](Program.cs) - Full working example
- [ManualConsumerService.cs](ManualConsumerService.cs) - Service that manually consumes messages
- Main README - Overview of the messaging framework
