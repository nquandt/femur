# Femur.Logging.Bootstrap

A lightweight .NET library that enables logging during application startup, before the host is fully initialized.

## The Problem

In .NET applications using the Generic Host (`Host.CreateApplicationBuilder`), logging typically isn't available until after the host is built. This creates a blind spot during critical startup phases:

```csharp
var builder = Host.CreateApplicationBuilder(args);
// No logging available here!

builder.Services.AddHostedService<MyService>();
// Or here...

var host = builder.Build();
// Logging only works after this point
await host.RunAsync();
```

**Femur.Logging.Bootstrap** solves this by providing a standalone logger that works immediately and seamlessly transitions to your application's main logging infrastructure.

## Features

- **Early logging**: Log during host configuration and startup validation
- **Seamless integration**: Bootstrap logger transitions cleanly into the main application host
- **Zero duplication**: Logging providers are shared between bootstrap and host - logs go to the same destination
- **Proper disposal**: Ensures logger providers are flushed on application shutdown
- **OpenTelemetry support**: Works with OpenTelemetry logging, tracing, and metrics
- **Simple API**: Minimal code changes to add bootstrap logging to existing applications

## Installation

```bash
dotnet add package Femur.Logging.Bootstrap
```

## Quick Start

```csharp
using Femur.Logging.Bootstrap;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Create a bootstrap logger before anything else
using var logger = BootstrapLogger.Create<Program>(builder =>
{
    builder.ClearProviders();
    builder.AddConsole();
});

// Now you can log during startup!
logger.LogInformation("Starting application with bootstrapped logging.");

var builder = Host.CreateApplicationBuilder(args);

logger.LogInformation("Configuring services.");

// Transfer the bootstrap logger to the host
builder.Services.AddBootstrappedLogging(logger);

var host = builder.Build();

logger.LogInformation("Starting application.");

await host.RunAsync();

logger.LogInformation("Application stopped.");
```

## How It Works

1. **Create**: `BootstrapLogger.Create<T>()` creates a standalone logger with your configured providers
2. **Use**: The logger works immediately - use it anywhere before host initialization
3. **Transfer**: `AddBootstrappedLogging()` transfers the logging infrastructure to your main host
4. **Share**: Both loggers write to the same providers - no duplication

The bootstrap logger's service provider is kept alive and shared with the main host, ensuring:
- Log messages from both stages appear in the same output
- OpenTelemetry traces and metrics are correlated correctly
- Logger providers are properly flushed on shutdown

## Use Cases

### Startup Validation

```csharp
using var logger = BootstrapLogger.Create<Program>(builder =>
{
    builder.AddConsole();
});

logger.LogInformation("Validating environment...");

if (!Directory.Exists("/required/path"))
{
    logger.LogCritical("Required directory not found!");
    return 2; // Exit with error code
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBootstrappedLogging(logger);
// ... continue with host setup
```

### Configuration Debugging

```csharp
using var logger = BootstrapLogger.Create<Program>(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

logger.LogDebug("Loading configuration from {Path}", configPath);

var builder = Host.CreateApplicationBuilder(args);
logger.LogDebug("Configuration sections: {Sections}",
    string.Join(", ", builder.Configuration.AsEnumerable().Select(c => c.Key)));

builder.Services.AddBootstrappedLogging(logger);
// ... continue
```

### OpenTelemetry Integration

```csharp
using var logger = BootstrapLogger.Create<Program>(
    builder =>
    {
        builder.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("MyService"));
            options.AddConsoleExporter();
        });
    },
    services =>
    {
        // Register additional services needed by OpenTelemetry
        services.AddSingleton(new ActivitySource("MyService"));
    });

logger.LogInformation("Application starting with OpenTelemetry");

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBootstrappedLogging(logger);

// Add tracing and metrics
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("MyService")
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddRuntimeInstrumentation()
        .AddConsoleExporter());

var host = builder.Build();
await host.RunAsync();
```

## Examples

This package includes two complete examples in the repository:

- **BasicExample**: Simple console application with bootstrap logging and a hosted service
- **AdvancedExample**: Production-ready patterns with OpenTelemetry, distributed tracing, error handling, and validation

## API Reference

### BootstrapLogger.Create&lt;T&gt;

Creates a new bootstrap logger with an explicit type parameter.

```csharp
BootstrapLogger Create<T>(
    Action<ILoggingBuilder> configure,
    Action<IServiceCollection>? configureServices = null)
```

**Parameters:**
- `configure`: Configure logging providers (Console, OpenTelemetry, etc.)
- `configureServices`: Optional callback to register additional services (e.g., ActivitySource for OpenTelemetry)

**Returns:** A `BootstrapLogger` that implements `ILogger`, `IDisposable`, and `IAsyncDisposable`

### AddBootstrappedLogging

Extension method to transfer bootstrap logging to the host.

```csharp
IServiceCollection AddBootstrappedLogging(
    this IServiceCollection services,
    BootstrapLogger logger)
```

**Parameters:**
- `services`: The host's service collection
- `logger`: The bootstrap logger to transfer

**Returns:** The service collection for chaining

## Integration with Femur.Hosting

If you're using the Femur.Hosting framework, bootstrap logging is built-in. The framework automatically creates a bootstrap logger with type discovery, uses it during startup, and seamlessly transfers it to your application's host:

```csharp
using Femur.Hosting;

// The framework handles bootstrap logging automatically
var result = await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()  // Bootstrap logger is created here
    .SkipConfiguration()
    .ConfigureServices(services =>
    {
        services.AddHostedService<MyWorkerService>();
    })
    .SkipConfigureErrorHandlers()
    .RunAsync();

return result;
```

The Femur.Hosting framework provides comprehensive error handling, graceful shutdown, and detailed logging throughout the application lifecycle, all built on top of this bootstrap logging infrastructure. Type discovery for logger categories is handled internally by the hosting framework.

## Requirements

- .NET 8.0 or later (also supports .NET Standard 2.0)
- Microsoft.Extensions.Logging 8.0+
- Microsoft.Extensions.DependencyInjection 8.0+
- Femur.DependencyInjection (automatically included)

## License

MIT
