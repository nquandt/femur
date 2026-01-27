# Getting Started with Femur

Welcome to Femur! This guide will help you build your first application in under 10 minutes.

## Installation

Install the Femur.Hosting package to get started with building applications:

```bash
dotnet new console -n MyFirstFemurApp
cd MyFirstFemurApp
dotnet add package Femur.Hosting
dotnet add package FluentValidation
```

## Your First Application

Create a simple console application that demonstrates Femur's core features:

```csharp
using Femur.Hosting;
using FluentValidation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Create and run the application
return await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<HelloWorldService>();
    })
    .RunAsync();

// Simple hosted service that runs on startup
class HelloWorldService : IHostedService
{
    private readonly ILogger<HelloWorldService> _logger;

    public HelloWorldService(ILogger<HelloWorldService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hello from Femur! Application started successfully.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Application stopping. Goodbye!");
        return Task.CompletedTask;
    }
}
```

Run your application:

```bash
dotnet run
```

You should see output like:

```
info: HelloWorldService[0]
      Hello from Femur! Application started successfully.
```

Press Ctrl+C to stop the application gracefully.

## What Just Happened?

Let's break down the key components:

1. **ApplicationBuilder.Create(args)** - Creates a fluent builder for your application
2. **UseDefaultConsoleLogging()** - Sets up bootstrap logging (logs before host fully initialized)
3. **ConfigureServices()** - Configures dependency injection container
4. **RunAsync()** - Builds and runs the application, returning an exit code

The application uses a `IHostedService` which automatically starts when the host starts and stops when the host shuts down gracefully.

## Adding Configuration with Validation

Now let's add strongly-typed configuration with automatic validation:

```csharp
using Femur;
using Femur.Hosting;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

return await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()
    .ConfigureConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false);
    })
    .ConfigureServices((context, services) =>
    {
        // Register options with validation
        services.TryConfigureByConventionWithValidation<AppSettings>();
        services.AddHostedService<GreetingService>();
    })
    .RunAsync();

// Strongly-typed configuration with validation
class AppSettings : IStandardOptions<AppSettings>
{
    public static string SectionName => "App";

    public string GreetingMessage { get; set; } = "";
    public int RepeatCount { get; set; } = 1;

    public static void SetupValidator(AbstractValidator<AppSettings> validator)
    {
        validator.RuleFor(x => x.GreetingMessage).NotEmpty();
        validator.RuleFor(x => x.RepeatCount).GreaterThan(0).LessThanOrEqualTo(10);
    }
}

class GreetingService : IHostedService
{
    private readonly AppSettings _settings;
    private readonly ILogger<GreetingService> _logger;

    public GreetingService(IOptions<AppSettings> options, ILogger<GreetingService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        for (int i = 0; i < _settings.RepeatCount; i++)
        {
            _logger.LogInformation(_settings.GreetingMessage);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Create an `appsettings.json` file:

```json
{
  "App": {
    "GreetingMessage": "Welcome to Femur!",
    "RepeatCount": 3
  }
}
```

Run the application and see the greeting repeated 3 times. Try changing `RepeatCount` to 0 or 20 and see validation errors at startup!

## Key Concepts Quick Reference

### ApplicationBuilder Lifecycle Stages

The ApplicationBuilder follows a structured lifecycle:

1. **Initial** - Create builder with args
2. **Bootstrap** - Set up bootstrap logging (logs before host initialization)
3. **Configuration** - Load configuration files and environment variables
4. **Services** - Register services in dependency injection container
5. **Executable** - Build and run the application

Each stage can only be configured once, and the fluent API guides you through the correct order.

### IStandardOptions Pattern

Implement `IStandardOptions<TOptions>` for strongly-typed configuration:

- **SectionName** - Configuration section to bind (e.g., "App", "Database", "Features")
- **SetupValidator** - FluentValidation rules applied at startup
- **TryConfigureByConventionWithValidation()** - Automatically binds and validates

Validation happens on `ValidateOnStart()`, catching configuration errors before the application runs.

### Bootstrap Logging

Bootstrap logging solves the "blind spot" problem:

- Traditional .NET apps can't log during configuration phase
- BootstrapLogger creates a lightweight logger BEFORE host initialization
- Logs configuration loading, validation, and service registration
- Same logging providers shared with main host (no duplication)

### Exit Codes

Femur uses standardized exit codes for operational integration:

| Exit Code | Meaning |
|-----------|---------|
| 0 | Success |
| 1 | Builder creation failed |
| 2 | Build failed (configuration or services) |
| 3 | Runtime error |
| 4 | Pre-startup error |
| 5 | Post-shutdown error |
| 10 | Bootstrap logger failed |
| 125 | Command cancelled |
| 130 | Ctrl+C interrupt |

These codes integrate with orchestrators, CI/CD pipelines, and monitoring systems.

## Error Handling

Add error handlers for different lifecycle phases:

```csharp
return await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()
    .ConfigureServices((context, services) => { /* ... */ })
    .OnBuildError((ex, logger) =>
    {
        logger.LogCritical(ex, "Failed to build application");
        return ExitCodes.BuildFailed;
    })
    .OnRuntimeError((ex, logger) =>
    {
        logger.LogCritical(ex, "Runtime error occurred");
        return ExitCodes.RuntimeError;
    })
    .RunAsync();
```

This ensures graceful error handling at each stage with appropriate logging and exit codes.

## Next Steps

Now that you understand the basics, explore more advanced features:

- **[API Data Aggregator Example](examples/api-aggregator/README.md)** - Complete application demonstrating hosting, validation, logging, and serialization
- **[Core Concepts](core-concepts.md)** - Deep dive into Femur's architecture and design principles
- **[Logging Examples](../src/Logging/examples/)** - Advanced bootstrap logging with OpenTelemetry integration
- **[Module Reference](README.md#module-reference)** - Explore all available packages

### Additional Packages to Explore

Depending on your needs, you may want to add:

```bash
# For parsing HTML, XML, or Markdown
dotnet add package Femur.Html.Parser
dotnet add package Femur.Markdown.Parser
dotnet add package Femur.Markdown.Renderer

# For serialization
dotnet add package Femur.Serialization

# For file system abstractions
dotnet add package Femur.FileSystem
dotnet add package Femur.FileSystem.AzureBlob

# For web applications
dotnet add package Femur.Hosting.Web
dotnet add package Femur.AspNetCore
```

Check out the [full documentation](README.md) for detailed guides on each package.
