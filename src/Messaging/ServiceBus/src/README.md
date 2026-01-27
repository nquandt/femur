# Femur.Messaging.ServiceBus

Azure Service Bus transport implementation for Femur.Messaging.

## Installation

```bash
dotnet add package Femur.Messaging.ServiceBus
```

## Usage

### Configuration

Configure Azure Service Bus in your `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "ServiceBus": "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=..."
  },
  "ServiceBus": {
    "QueueName": "orders"
  }
}
```

### Registration

**Recommended: Using IConfiguration (DI Resolution Time)**

This approach lets dependency injection resolve your configuration, supporting IOptions, named options, and configuration reloading:

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Register message handler and processor
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>()
    .UseTransport() // Use default transport
    .Configure(options =>
    {
        options.MaxConcurrentMessages = 5;
        options.MaxRetries = 3;
    });

// Add Service Bus transport - connection string resolved from IConfiguration at runtime
builder.Services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("ServiceBus")!,
    configure: options => options.QueueName = builder.Configuration["ServiceBus:QueueName"]!);

var host = builder.Build();
await host.RunAsync();
```

**Using IOptions Pattern**

```csharp
// Configure options
builder.Services.Configure<ServiceBusConfig>(builder.Configuration.GetSection("ServiceBus"));

// Add Service Bus with options resolution
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>();

builder.Services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IOptions<ServiceBusConfig>>().Value.ConnectionString,
    configure: options =>
    {
        var config = builder.Configuration.GetSection("ServiceBus").Get<ServiceBusConfig>();
        options.QueueName = config!.QueueName;
    });
```

**Direct Connection String (Simple Cases)**

```csharp
// For simple scenarios where configuration is static
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>();
builder.Services.AddFemurServiceBus(
    "Endpoint=sb://your-namespace.servicebus.windows.net/;...",
    configure: options => options.QueueName = "orders");
```

## Authentication

The Service Bus transport uses `DefaultAzureCredential` for authentication, which supports:
- Managed Identity (in Azure)
- Azure CLI credentials (local development)
- Environment variables
- Visual Studio/VS Code credentials

For local development, ensure you're logged in with Azure CLI:

```bash
az login
```

## Configuration Options

```csharp
public class ServiceBusOptions
{
    public string FullyQualifiedNamespace { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
}
```

## Features

- Automatic message settlement (complete/abandon/dead-letter)
- Session support
- Dead-letter queue handling
- Retry policies
- Managed Identity authentication

## License

MIT
