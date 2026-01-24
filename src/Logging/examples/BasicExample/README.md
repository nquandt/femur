# Basic Example

A minimal example demonstrating the core functionality of LoggingBootstrap in a simple console application.

## What This Example Shows

- Creating a bootstrap logger before host initialization
- Logging during the startup phase
- Transferring the bootstrap logger to the main application host
- Using the logger in a hosted service
- Proper logger disposal on shutdown

## The Code

This example creates a simple hosted service that logs a message every 5 seconds. The bootstrap logger is used to log messages during the entire application lifecycle:

1. **Before host creation** - "Starting application with bootstrapped logging."
2. **During configuration** - "Configuring services."
3. **Before host runs** - "Starting application."
4. **During execution** - The hosted service logs "Service running at {Time}"
5. **After shutdown** - "Application stopped."

## Running the Example

```bash
dotnet run --project examples/BasicExample
```

**Expected output:**
```
info: Program[0]
      Starting application with bootstrapped logging.
info: Program[0]
      Configuring services.
info: Program[0]
      Starting application.
info: ExampleHostedService[0]
      Service running at 01/22/2026 10:30:00
info: ExampleHostedService[0]
      Service running at 01/22/2026 10:30:05
info: ExampleHostedService[0]
      Service running at 01/22/2026 10:30:10
^C
info: Program[0]
      Application stopped.
```

Press `Ctrl+C` to stop the application gracefully.

## Key Concepts

### Bootstrap Logger Creation

```csharp
using var logger = BootstrapLogger.Create<Program>(builder =>
{
    builder.ClearProviders();
    builder.AddConsole();
});
```

Creates a standalone logger that works immediately, before the host is initialized.

### Transfer to Host

```csharp
builder.Services.AddBootstrappedLogging(logger);
```

Transfers the bootstrap logger's infrastructure to the main application host, ensuring logs continue to the same destination.

### Dependency Injection

```csharp
class ExampleHostedService : BackgroundService
{
    private readonly ILogger<ExampleHostedService> _logger;

    public ExampleHostedService(ILogger<ExampleHostedService> logger)
    {
        _logger = logger;
    }
    // ...
}
```

Services use standard `ILogger<T>` dependency injection - they automatically use the bootstrapped logging infrastructure.

## Next Steps

For more advanced patterns, see the [AdvancedExample](../AdvancedExample/), which includes:
- OpenTelemetry integration with distributed tracing
- Startup validation and error handling
- Multiple coordinated services
- Retry logic and health monitoring
- Production-ready patterns
