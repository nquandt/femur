# Femur.Hosting

A fluent builder framework for console applications with structured lifecycle management, comprehensive error handling, and bootstrap logging support.

## Installation

```bash
dotnet add package Femur.Hosting
```

## Quick Example

```csharp
using Femur.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

return await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<MyService>();
    })
    .RunAsync();

class MyService : IHostedService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger) => _logger = logger;

    public Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

## Key Features

### Staged Fluent Builder

ApplicationBuilder uses a staged fluent API that guides you through configuration in the correct order:

1. **Initial** → Create builder with args
2. **Bootstrap** → Set up logging (before host initialization)
3. **Configuration** → Load config files, environment variables
4. **Services** → Register services in DI container
5. **Executable** → Build and run the application

Each stage returns a different interface, ensuring type-safe configuration and preventing incorrect usage.

### Comprehensive Error Handling

Handle errors at different lifecycle phases with specific handlers:

```csharp
await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()
    .ConfigureServices(...)
    .OnBuilderError((ex, logger) => ExitCodes.BuilderCreationFailed)
    .OnBuildError((ex, logger) => ExitCodes.BuildFailed)
    .OnPreStartupError((ex, logger) => ExitCodes.PreStartupError)
    .OnRuntimeError((ex, logger) => ExitCodes.RuntimeError)
    .OnPostShutdownError((ex, logger) => ExitCodes.PostShutdownError)
    .RunAsync();
```

### Exit Codes

Standardized exit codes for operational integration:

| Exit Code | Constant | Meaning |
|-----------|----------|---------|
| 0 | `Success` | Application completed successfully |
| 1 | `BuilderCreationFailed` | Failed to create ApplicationBuilder |
| 2 | `BuildFailed` | Failed during configuration or service registration |
| 3 | `RuntimeError` | Unhandled exception during execution |
| 4 | `PreStartupError` | Failed during host startup initialization |
| 5 | `PostShutdownError` | Error during disposal/cleanup |
| 10 | `BootstrapLoggerFailed` | Bootstrap logger initialization failed |
| 125 | `CommandCancelled` | User cancelled operation |
| 130 | `CtrlCInterrupt` | Ctrl+C interrupt signal |

These codes enable Docker, Kubernetes, CI/CD pipelines, and monitoring tools to understand failure reasons.

### Bootstrap Logging Integration

Bootstrap logging solves the "blind spot" in .NET Generic Host where logging isn't available during configuration:

```csharp
await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()  // Creates BootstrapLogger
    .ConfigureConfiguration((context, config) =>
    {
        // Logs: "Loading configuration..."
        config.AddJsonFile("appsettings.json");
    })
    .ConfigureServices((context, services) =>
    {
        // Logs: "Registering services..."
        services.AddHostedService<MyService>();
    })
    .RunAsync();
```

The same logging providers (console, OpenTelemetry, etc.) receive logs from both bootstrap and runtime phases.

### Type Discovery

Automatic discovery of the `Program` type for logger category naming:

```csharp
// Logger category automatically resolved to "Program" or entry assembly name
.UseDefaultConsoleLogging()

// Equivalent to:
.UseDefaultConsoleLogging<Program>()
```

## Complete Example

```csharp
using Femur;
using Femur.Hosting;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

return await ApplicationBuilder.Create(args)
    // Stage 1: Bootstrap logging
    // Logs configuration and service registration
    .UseDefaultConsoleLogging()

    // Stage 2: Configuration
    // Load configuration files
    .ConfigureConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false);
        config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
        config.AddEnvironmentVariables();
    })

    // Stage 3: Services
    // Register services with DI container
    .ConfigureServices((context, services) =>
    {
        // Configuration with validation
        services.TryConfigureByConventionWithValidation<AppOptions>();

        // Business logic
        services.AddHostedService<WorkerService>();
    })

    // Error handling for each lifecycle phase
    .OnBuildError((exception, logger) =>
    {
        logger.LogCritical(exception, "Configuration or validation failed");
        return ExitCodes.BuildFailed;
    })
    .OnRuntimeError((exception, logger) =>
    {
        logger.LogCritical(exception, "Unhandled runtime exception");
        return ExitCodes.RuntimeError;
    })

    // Stage 4: Run
    .RunAsync();

// Configuration with validation
class AppOptions : IStandardOptions<AppOptions>
{
    public static string SectionName => "App";
    public string WorkerName { get; set; } = "";

    public static void SetupValidator(AbstractValidator<AppOptions> validator)
    {
        validator.RuleFor(x => x.WorkerName).NotEmpty();
    }
}

// Worker service
class WorkerService : IHostedService
{
    private readonly AppOptions _options;
    private readonly ILogger<WorkerService> _logger;

    public WorkerService(IOptions<AppOptions> options, ILogger<WorkerService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker {Name} started", _options.WorkerName);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker stopped");
        return Task.CompletedTask;
    }
}
```

## Architecture

### ApplicationBuilder Stages

The builder enforces a specific configuration order through interface progression:

```
IInitialApplicationBuilder           Create(args)
    ↓
IBootstrapApplicationBuilder         UseDefaultConsoleLogging()
    ↓
IConfigurationApplicationBuilder     ConfigureConfiguration(...)
    ↓
IServicesApplicationBuilder          ConfigureServices(...)
    ↓
IExecutableApplicationBuilder        OnBuildError(...), OnRuntimeError(...), RunAsync()
```

Each interface only exposes methods valid for that stage, preventing configuration mistakes.

### Error Handling Strategy

Errors are differentiated by lifecycle phase:

1. **Builder Creation** → Before ApplicationBuilder exists
2. **Build Phase** → During configuration and service registration
3. **Pre-Startup** → During `host.RunAsync()` initialization
4. **Runtime** → During normal application execution
5. **Post-Shutdown** → During disposal and cleanup

Each phase has a dedicated error handler returning an appropriate exit code.

## Use Cases

### Console Applications

Build structured console applications with proper lifecycle management:

```csharp
await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()
    .ConfigureServices(services => services.AddHostedService<ConsoleWorker>())
    .RunAsync();
```

### CLI Tools

Command-line tools with validation and error handling:

```csharp
await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()
    .ConfigureServices(services =>
    {
        services.TryConfigureByConventionWithValidation<CliOptions>();
        services.AddHostedService<CliExecutor>();
    })
    .OnBuildError((ex, logger) => ExitCodes.BuildFailed)
    .RunAsync();
```

### Scheduled Jobs

Long-running background workers:

```csharp
await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()
    .ConfigureServices(services =>
    {
        services.AddHostedService<ScheduledJobWorker>();
        services.AddSingleton<IJobScheduler, QuartzScheduler>();
    })
    .RunAsync();
```

## Web Applications

For ASP.NET Core applications, use **Femur.Hosting.Web** which provides `WebApplicationBuilder` with similar patterns for web scenarios.

## See Also

- **[Full Documentation](../../../docs/README.md)** - Complete Femur documentation
- **[Getting Started Guide](../../../docs/getting-started.md)** - Step-by-step tutorial
- **[API Data Aggregator Example](../../../docs/examples/api-aggregator/README.md)** - Complete working application
- **[Core Concepts](../../../docs/core-concepts.md)** - Architecture and design principles
- **[Femur.Logging.Bootstrap](../../Logging/Bootstrap/src/README.md)** - Bootstrap logging details
- **[Logging Examples](../../Logging/examples/)** - Advanced logging patterns

## Package Dependencies

- `Microsoft.Extensions.Hosting` - Generic host infrastructure
- `Microsoft.Extensions.Logging` - Logging abstractions
- `Microsoft.Extensions.Configuration` - Configuration system
- `Microsoft.Extensions.DependencyInjection` - DI container

## Target Frameworks

- .NET 8.0 (LTS)
- .NET 9.0 (STS)
- .NET 10.0