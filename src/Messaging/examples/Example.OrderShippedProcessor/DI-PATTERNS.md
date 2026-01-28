# Dependency Injection Patterns with Femur

This guide explains proper dependency injection patterns when using Femur, especially when working with configuration values during service registration.

## The Anti-Pattern: Building Temporary Service Providers

### ❌ NEVER Do This

```csharp
.ConfigureServices(services =>
{
    // ANTI-PATTERN: Building a temporary service provider
    using var tempProvider = services.BuildServiceProvider();
    var config = tempProvider.GetRequiredService<IConfiguration>();

    var someValue = config.GetValue<string>("SomeSetting");
    services.AddSingleton<IMyService>(new MyService(someValue));
})
```

### Why This Is Bad

1. **Disposed Resources**: The temporary service provider is disposed immediately, but registered services might hold references to it
2. **Double Registration**: Services registered before building the temp provider are registered twice
3. **Performance**: Building service providers is expensive
4. **Lifecycle Issues**: Singleton services from the temp provider are different instances than the final provider
5. **Validation Bypass**: Configuration validation doesn't run properly

## Proper Patterns

### Pattern 1: Factory Pattern (Preferred)

**Use this when**: You need configuration values at service creation time

```csharp
.ConfigureServices(services =>
{
    // ✅ Factory pattern: IConfiguration is resolved when the service is created
    services.AddSingleton<IMyService>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var someValue = config.GetValue<string>("SomeSetting");
        return new MyService(someValue);
    });
})
```

**Example from this project**:
```csharp
services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("ServiceBus connection string not configured"));
```

### Pattern 2: IOptions Pattern (Strongly Recommended)

**Use this when**: You have a configuration section that maps to a settings class

```csharp
// Define your options class
public class MyServiceOptions
{
    public string SomeSetting { get; set; }
    public int Timeout { get; set; }
}

.ConfigureServices(services =>
{
    // ✅ Options pattern: Configuration binding happens at resolution time
    services.AddOptions<MyServiceOptions>()
        .Configure<IConfiguration>((options, config) =>
        {
            config.GetSection("MyService").Bind(options);
        });

    // Service receives IOptions<T> via constructor injection
    services.AddSingleton<IMyService, MyService>();
})

// In your service
public class MyService : IMyService
{
    private readonly MyServiceOptions _options;

    public MyService(IOptions<MyServiceOptions> options)
    {
        _options = options.Value;
    }
}
```

**Example from this project**:
```csharp
services.AddOptions<MockEmailServiceOptions>()
    .Configure<IConfiguration>((options, config) =>
    {
        config.GetSection("EmailService").Bind(options);
    });
services.AddSingleton<IEmailService, MockEmailService>();
```

### Pattern 3: Environment Variables for Registration-Time Decisions

**Use this when**: You need to make decisions at registration time (e.g., which transport to register)

```csharp
.ConfigureServices(services =>
{
    // ✅ Read environment variables directly (not through IConfiguration)
    var useInMemory = Environment.GetEnvironmentVariable("USE_INMEMORY_TRANSPORT")?.ToLowerInvariant() == "true";

    if (useInMemory)
    {
        services.AddFemurInMemory();
    }
    else
    {
        // Note: Connection string still uses factory pattern for IConfiguration
        services.AddFemurServiceBus(
            sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("ServiceBus")
                ?? throw new InvalidOperationException("Connection string required"));
    }
})
```

**Why this works**: Environment variables can be read directly without DI, and they're available before service registration.

### Pattern 4: Runtime Configuration Changes

**Use this when**: Configuration can change at runtime and you need to react to changes

For runtime configuration changes, use `IOptionsMonitor<T>` or `IOptionsSnapshot<T>`, **not** `IConfiguration`:

```csharp
.ConfigureServices(services =>
{
    services.AddOptions<MyServiceOptions>()
        .Configure<IConfiguration>((options, config) =>
        {
            config.GetSection("MyService").Bind(options);
        });

    services.AddSingleton<IMyService, MyService>();
})

public class MyService : IMyService
{
    private readonly IOptionsMonitor<MyServiceOptions> _optionsMonitor;

    public MyService(IOptionsMonitor<MyServiceOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;

        // Optional: React to configuration changes
        _optionsMonitor.OnChange(newOptions =>
        {
            // Configuration was reloaded, react to changes
        });
    }

    public void DoWork()
    {
        // Always gets the current configuration value
        var options = _optionsMonitor.CurrentValue;
        var value = options.SomeSetting;
    }
}
```

**Options Types Summary**:
- `IOptions<T>`: Static configuration (no reload support). Singleton lifetime.
- `IOptionsSnapshot<T>`: Per-request configuration snapshots. Scoped lifetime. Use in ASP.NET Core when you want consistent config per request.
- `IOptionsMonitor<T>`: Real-time configuration changes. Singleton lifetime. Use when you need to react to `reloadOnChange: true`.

### Pattern 5: Direct IConfiguration Access (Rare)

**Use this when**: You need dynamic key access or the configuration structure is unpredictable

```csharp
public class DynamicConfigService : IDynamicConfigService
{
    private readonly IConfiguration _config;

    public DynamicConfigService(IConfiguration config)
    {
        _config = config;
    }

    public string GetDynamicValue(string key)
    {
        // Dynamic key access - can't use IOptions for this
        return _config.GetValue<string>(key);
    }
}
```

**Caution**: Injecting `IConfiguration` directly is generally an anti-pattern. Prefer strongly-typed `IOptions<T>` for most scenarios. Only use `IConfiguration` when you truly need dynamic key access.

## Special Case: Conditional Service Registration

When you need to conditionally register services based on configuration, you have limited options:

### Option A: Environment Variables (Recommended)

```csharp
var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
if (isDevelopment)
{
    services.AddSingleton<IEmailService, MockEmailService>();
}
else
{
    services.AddSingleton<IEmailService, SendGridEmailService>();
}
```

### Option B: Register Both, Select at Runtime

```csharp
// Register both implementations
services.AddSingleton<MockEmailService>();
services.AddSingleton<SendGridEmailService>();

// Factory decides which to use based on configuration (resolved at runtime)
services.AddSingleton<IEmailService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var useMock = config.GetValue<bool>("UseMockEmail");

    return useMock
        ? sp.GetRequiredService<MockEmailService>()
        : sp.GetRequiredService<SendGridEmailService>();
});
```

### Option C: Separate Entry Points

Create separate `Program.cs` files or use conditional compilation:

```csharp
#if DEBUG
    services.AddFemurInMemory();
#else
    services.AddFemurServiceBus(sp => ...);
#endif
```

## Configuration Validation

### ✅ Validate Options at Startup

```csharp
services.AddOptions<MyServiceOptions>()
    .Configure<IConfiguration>((options, config) =>
    {
        config.GetSection("MyService").Bind(options);
    })
    .ValidateDataAnnotations()  // Validate [Required], [Range], etc.
    .ValidateOnStart();          // Fail fast at startup, not first use
```

### ✅ Use FluentValidation with Femur

```csharp
public class MyServiceOptionsValidator : AbstractValidator<MyServiceOptions>
{
    public MyServiceOptionsValidator()
    {
        RuleFor(x => x.Timeout)
            .GreaterThan(0)
            .LessThan(300);
    }
}

services.AddOptions<MyServiceOptions>()
    .Configure<IConfiguration>((options, config) =>
    {
        config.GetSection("MyService").Bind(options);
    })
    .ValidateFluentValidation()
    .ValidateOnStart();
```

## Summary

| Scenario | Recommended Pattern | Example |
|----------|-------------------|---------|
| Need config for service creation | Factory Pattern | `services.AddSingleton<IService>(sp => { var config = sp.GetRequiredService<IConfiguration>(); ... })` |
| Configuration section → Settings class (static) | IOptions<T> Pattern | `services.AddOptions<T>().Configure<IConfiguration>((opts, cfg) => cfg.GetSection("...").Bind(opts))` |
| Registration-time decisions | Environment Variables | `var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";` |
| Runtime configuration changes (singleton) | IOptionsMonitor<T> | `public MyService(IOptionsMonitor<T> optionsMonitor)` |
| Runtime configuration changes (scoped/request) | IOptionsSnapshot<T> | `public MyController(IOptionsSnapshot<T> optionsSnapshot)` |
| Dynamic key access (rare) | Inject IConfiguration | `public MyService(IConfiguration config)` - Avoid if possible |
| Conditional registration | Env vars or dual registration | See Option A or B above |

## Key Principles

1. **Never build temporary service providers** during registration
2. **Defer configuration reading** to resolution time when possible (factory/options patterns)
3. **Use environment variables** for registration-time decisions
4. **Validate options** at startup with `.ValidateOnStart()`
5. **Prefer IOptions/IOptionsMonitor/IOptionsSnapshot** over injecting IConfiguration directly
6. **Use IOptionsMonitor** for runtime configuration changes, not IConfiguration
7. **Document which environment variables** are used for configuration

## Real-World Example

```csharp
return await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()
    .ConfigureConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json");
        config.AddEnvironmentVariables();
    })
    .ConfigureServices(services =>
    {
        // ✅ Options pattern for service settings
        services.AddOptions<EmailServiceOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection("Email").Bind(options);
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ✅ Environment variable for transport selection
        var useInMemory = Environment.GetEnvironmentVariable("USE_INMEMORY") == "true";
        if (useInMemory)
        {
            services.AddFemurInMemory();
        }
        else
        {
            // ✅ Factory pattern for connection string
            services.AddFemurServiceBus(
                sp => sp.GetRequiredService<IConfiguration>()
                    .GetConnectionString("ServiceBus")
                    ?? throw new InvalidOperationException("Connection string required"));
        }

        services.AddMessageHandler<OrderShippedMessage, OrderShippedHandler>();
    })
    .SkipConfigureErrorHandlers()
    .RunAsync();
```

## Additional Resources

- [Microsoft: Options pattern in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)
- [Microsoft: Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Andrew Lock: How to use the IOptions pattern](https://andrewlock.net/how-to-use-the-ioptions-pattern-for-configuration-in-asp-net-core-rc2/)
