# Femur.Messaging.InMemory

In-memory transport implementation for Femur.Messaging. Perfect for testing and development.

## Installation

```bash
dotnet add package Femur.Messaging.InMemory
```

## Usage

### Registration

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Register your message handler
builder.Services.AddSingleton<IMessageHandler<OrderMessage>, OrderMessageHandler>();

// Add in-memory transport
builder.Services.AddInMemoryTransport();

// Add message processor
builder.Services.AddMessageProcessor<OrderMessage>(options =>
{
    options.MaxConcurrentMessages = 5;
});

var host = builder.Build();
await host.RunAsync();
```

### Sending Messages (for testing)

```csharp
// Get the transport
var transport = serviceProvider.GetRequiredService<IMessagingTransport>();

// If it's the in-memory transport, you can enqueue messages
if (transport is InMemoryTransport inMemoryTransport)
{
    var message = new OrderMessage 
    { 
        OrderId = "123", 
        Amount = 99.99m 
    };
    
    inMemoryTransport.Enqueue(message);
}
```

## Features

- Thread-safe in-memory message queue
- Message settlement (complete/abandon/dead-letter)
- Perfect for unit tests and integration tests
- No external dependencies

## Testing Example

```csharp
[Fact]
public async Task Handler_ProcessesMessage()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IMessageHandler<OrderMessage>, OrderMessageHandler>();
    services.AddInMemoryTransport();
    services.AddMessageProcessor<OrderMessage>();
    
    var provider = services.BuildServiceProvider();
    var transport = provider.GetRequiredService<InMemoryTransport>();
    
    // Act
    transport.Enqueue(new OrderMessage { OrderId = "123", Amount = 99.99m });
    
    // Start processing
    var hostedService = provider.GetRequiredService<IHostedService>();
    await hostedService.StartAsync(CancellationToken.None);
    
    // Wait for processing
    await Task.Delay(100);
    
    // Assert
    Assert.Empty(transport.GetQueue<OrderMessage>());
}
```

## License

MIT
