# Femur.DependencyInjection

A library for proxying services from one `ServiceProvider` into another `ServiceCollection`, preserving lifetimes and handling edge cases like open generics and singleton instances.

## Overview

This package allows you to share services across different DI containers by creating factory delegates that resolve from a source `ServiceProvider`. This is useful when you need to maintain service instances across container boundaries while preserving lifetimes and instance sharing.

## Installation

```bash
dotnet add package Femur.DependencyInjection
```

## Basic Usage

```csharp
using Femur.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

// Create a source container with some services
var sourceServices = new ServiceCollection();
sourceServices.AddSingleton<IMyService, MyService>();
sourceServices.AddScoped<IOtherService, OtherService>();
var sourceProvider = sourceServices.BuildServiceProvider();

// Create a target container and proxy the services
var targetServices = new ServiceCollection();
targetServices.AddProxiedServices(sourceServices, sourceProvider);

// Build the target provider
var targetProvider = targetServices.BuildServiceProvider();

// Services resolved from target provider come from source provider
var service = targetProvider.GetRequiredService<IMyService>();
```

## How It Works

The `AddProxiedServices` extension method analyzes each service descriptor and handles it appropriately:

### 1. Open Generic Types
Services registered with open generic types (e.g., `IOptions<>`) are copied as-is to preserve their implementation type:

```csharp
sourceServices.AddSingleton(typeof(IOptions<>), typeof(OptionsManager<>));
```

### 2. Singleton Instances
Services registered with `ImplementationInstance` are copied directly to ensure the same instance is used:

```csharp
var instance = new MyService();
sourceServices.AddSingleton<IMyService>(instance);
// The exact same instance will be available in the target container
```

### 3. Factory Functions
By default, services registered with factory functions are proxied to resolve from the source provider, ensuring singleton instances are shared:

```csharp
sourceServices.AddSingleton<IMyService>(_ => new MyService());
// The target provider will resolve the same singleton instance from the source
```

### 4. Regular Types
All other services are registered with factory delegates that resolve from the source provider:

```csharp
sourceServices.AddTransient<IMyService, MyService>();
// Resolved via factory: _ => sourceProvider.GetRequiredService<IMyService>()
```

## Advanced Options

### Filtering Services

You can filter which services to proxy using the `ShouldSkipService` predicate:

```csharp
var options = new ProxyOptions
{
    ShouldSkipService = descriptor => descriptor.ServiceType == typeof(ILoggerProvider)
};

targetServices.AddProxiedServices(sourceServices, sourceProvider, options);
```

### Preserving Factory Behavior

By default, factory functions are proxied to share instances. If you want to preserve the original factory behavior (creating new instances):

```csharp
var options = new ProxyOptions
{
    PreserveExistingFactories = true
};

targetServices.AddProxiedServices(sourceServices, sourceProvider, options);
```

## Important Considerations

### Lifecycle Management

- The source `ServiceProvider` must remain alive for as long as the target provider needs to resolve services
- Disposing the source provider before the target provider can cause resolution failures
- Singleton instances are shared between both providers

### Service Resolution

- Services resolved from the target provider are actually resolved from the source provider
- This means service lifetimes are controlled by the source provider
- Scoped services will use the source provider's scope, not the target's

## Use Cases

This library is particularly useful for:

1. **Bootstrap Scenarios**: Setting up services before the main application container is built
2. **Service Sharing**: Sharing expensive singleton instances across container boundaries
3. **Gradual Migration**: Moving services between containers while maintaining compatibility
4. **Testing**: Creating isolated test containers that share specific services

## Example: Bootstrap Logger

```csharp
// Create a bootstrap logger with its own container
var bootstrapServices = new ServiceCollection();
bootstrapServices.AddLogging(builder => builder.AddConsole());
var bootstrapProvider = bootstrapServices.BuildServiceProvider();

// Use the logger during startup
var logger = bootstrapProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting up...");

// Later, transfer the logging services to the main container
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProxiedServices(bootstrapServices, bootstrapProvider);

// The main app uses the same logger instances from bootstrap
var app = builder.Build();
```

## License

MIT
