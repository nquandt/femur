---
title: "IOptions FluentValidation at Startup"
slug: "ioptions-fluentvalidation"
lang: en
description: "Move configuration validation to application startup using FluentValidation, catching errors before they reach your controllers."
---

ASP.NET Core's `IOptions<T>` pattern handles typed configuration well, but validation only happens when the DI container resolves those options. This means your application can start successfully with invalid configuration. Think missing API keys, malformed connection strings, out-of-range values. This means failures when code actually uses those options.

The following pattern moves validation to application startup using FluentValidation and `ValidateOnStart()`. If configuration is invalid, the app refuses to start. Configuration errors become immediate startup failures instead of runtime surprises.

## A Common Enough Example

Here's what a typical options pattern looks like:

:::C:Codeblock {lang="csharp"}
public class EmailOptions
{
    public string SmtpServer { get; set; }
    public int Port { get; set; }
    public string ApiKey { get; set; }
}

public class EmailService
{
    private readonly EmailOptions _options;
    
    public EmailService(IOptions<EmailOptions> options) // Validation triggers when IOptions resolves
    {
        _options = options.Value; // Often we'd manually validate here
    }
}
:::

Let's say `EmailService` only gets used by a notification endpoint. Maybe that endpoint doesn't get hit for hours after deployment. Maybe it's rarely used. Either way, your broken configuration sits there waiting to cause problems.

## Validate at Startup

What if instead we didn't have to wait to find out our options are missing. If config is invalid, lets stop the app from starting. Move the validation to startup.

This is especially useful in Kubernetes or similar orchestrators. They'll automatically restart failed containers (if you have health checks setup). In fact, they won't roll out bad deployments in the first place.

The pattern combines FluentValidation with ASP.NET Core's `ValidateOnStart()`. Validation runs during startup, before any HTTP requests arrive.

## Implementation

### Core Interfaces

First, you'll need some interfaces. These enforce a contract for all config classes:

:::C:Codeblock {lang="csharp" filename="IStandardOptions.cs" rawUrl="~/files/articles/ioptions-fluentvalidation/IStandardOptions.cs"}
:::

C# 11's static abstract members let us enforce this at compile time. Every config class must declare its section name. Every validatable config class must provide validation rules.

### Configuration Class Example

Here's what your config class looks like:

:::C:Codeblock {lang="csharp"}
public class EmailOptions : IStandardOptionsWithValidation<EmailOptions>
{
    public static string SectionName => "Email";
    
    public string SmtpServer { get; set; } = string.Empty;
    public int Port { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    
    public static void SetupValidator(AbstractValidator<EmailOptions> validator)
    {
        validator.RuleFor(x => x.SmtpServer)
            .NotEmpty()
            .Must(x => Uri.CheckHostName(x) != UriHostNameType.Unknown)
            .WithMessage("SmtpServer must be a valid hostname");
            
        validator.RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535);
            
        validator.RuleFor(x => x.ApiKey)
            .NotEmpty()
            .MinimumLength(32);
    }
}
:::

Now wire it up with these extension methods:

:::C:Codeblock {lang="csharp" filename="ServiceCollectionExtensions.cs" rawUrl="~/files/articles/ioptions-fluentvalidation/ServiceCollectionExtensions.cs"}
:::

The first method does the heavy lifting. It registers the validator, binds your config, hooks up FluentValidation, and enables startup validation. One call does everything.

### FluentValidation Integration

This adapter translates FluentValidation results into what IOptions expects:

:::C:Codeblock {lang="csharp" filename="FluentValidationOptions.cs" rawUrl="~/files/articles/ioptions-fluentvalidation/FluentValidationOptions.cs"}
:::

When validation fails, you get clear error messages. No generic "invalid configuration" exceptions. Each property violation gets spelled out.

## Usage in Program.cs

Using it is simple:

:::C:Codeblock {lang="csharp"}
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptionsWithValidation<EmailOptions>();

var app = builder.Build();

// Validation happens during Host.StartAsync() when you call app.Run()
app.Run();
:::

### When Validation Actually Runs

Here's something that tripped me up at first. Validation doesn't happen during `builder.Build()`. It happens when you call `app.Run()`.

Under the hood, `app.Run()` calls `Host.StartAsync()`. That's when `IStartupValidator` kicks in. Check the .NET Host source if you want to see the details.

The startup sequence looks like this:

1. `IHostLifetime.WaitForStartAsync` - Host readiness check
2. `IStartupValidator.Validate()` - Options validation (our code runs here)
3. `IHostedLifecycleService.StartingAsync` - Pre-startup hooks
4. `IHostedService.StartAsync` - Hosted services start
5. `IHostedLifecycleService.StartedAsync` - Post-startup hooks

Validation runs early in startup. But not until the host actually starts.

**Note**: .NET 9 changed how `IStartupValidator` works internally. Earlier versions behave differently. Check `OptionsBuilderExtensions.cs` in the dotnet/runtime repo to see how your version wires up `ValidateOnStart()`.

## Validating Earlier (Before Host Start)

Sometimes you want validation to happen even earlier. Maybe your orchestrator checks health before the app runs. Maybe you need validated config during initialization. Here's how I've been actually using `IStartupValidator` in production:

:::C:Codeblock {lang="csharp"}
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptionsWithValidation<EmailOptions>();

var app = builder.Build();

// Force validation right now
var validator = app.Services.GetService<IStartupValidator>();
if (validator is not null)
{
    validator.Validate(); // Throws if config is invalid
}

app.Run();
:::

**When you'd want this:**

- Container health checks - Orchestrator pings before your app runs
- Initialization dependencies - You need config before `Run()` executes
- Fast feedback - See errors immediately in logs

**Trade-offs to consider:**

Validation runs twice. Once manually, once during `StartAsync()`. Usually negligible. But if validation relies on hosted services, the early check might fail incorrectly.

### Alternative: Build ServiceProvider Twice (Not Recommended)

Some folks try building a temporary `ServiceProvider` before calling `builder.Build()`:

:::C:Codeblock {lang="csharp"}
// ⚠️ DON'T DO THIS
var tempSp = builder.Services.BuildServiceProvider();
var validator = tempSp.GetService<IStartupValidator>();
validator?.Validate();

var app = builder.Build(); // Builds service provider again
app.Run();
:::

Why this breaks things:

- Double initialization - Singletons get created twice
- Wasted resources - Building service providers is expensive
- Inconsistent behavior - Services might act differently between providers
- Framework warnings - ASP.NET Core explicitly discourages calling `BuildServiceProvider()` directly

**Better approach**: Validate manually after `app.Build()` if you need earlier validation.

## Why This Helps

**Containers fail to start** - Bad config prevents pods from becoming ready in Kubernetes. Bad deployments don't roll out.

**Local development is clearer** - Errors show up at startup. No more hunting through logs hours later.

**Production stays safe** - Config drift gets caught before any requests arrive. Missing environment variables? You'll know immediately.

**Error messages make sense** - FluentValidation gives you property-level details. Not vague "invalid configuration" messages.

**Proper lifecycle integration** - Uses `IStartupValidator` so validation happens after DI builds but before hosted services start.

## When You Shouldn't Use This

Sometimes strict startup validation isn't what you want:

**Multi-tenant apps** - One tenant's bad config shouldn't kill the whole application. Validate per-tenant instead.

**Graceful degradation** - Maybe your app works fine without email. If sending notifications is optional, don't fail startup for missing SMTP config.

**Monolithic services** - If one app handles multiple concerns (web API + background worker), valid config for one part shouldn't require valid config for both. Though honestly, this usually means you should split your services.

**Development environments** - Sometimes you want the app to run even with incomplete config. Use conditional registration:

:::C:Codeblock {lang="csharp"}
if (builder.Environment.IsProduction())
{
    builder.Services.AddOptionsWithValidation<EmailOptions>();
}
else
{
    builder.Services.AddOptions<EmailOptions>()
        .BindConfiguration(EmailOptions.SectionName);
}
:::

## Configuration vs. Dependency Health

Don't confuse these two things. They're different.

**Configuration validation** asks: "Is my config well-formed?"

- Is the SMTP port between 1 and 65535?
- Is the API key at least 32 characters?
- Is the connection string format valid?

**Dependency health checks** ask: "Can I reach external systems?"

- Can I connect to the database?
- Is the external API responding?
- Does the SMTP server accept my credentials?

### Use Health Checks for Connectivity

For checking external dependencies, use ASP.NET Core's Health Checks. Don't abuse options validation for this:

:::C:Codeblock {lang="csharp"}
var builder = WebApplication.CreateBuilder(args);

// Validate config structure
builder.Services.AddOptionsWithValidation<EmailOptions>();
builder.Services.AddOptionsWithValidation<DatabaseOptions>();

// Check external dependencies
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<EmailServiceHealthCheck>("email")
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!)
    .AddUrlGroup(new Uri("https://api.external.com/health"), "external-api");

var app = builder.Build();

app.MapHealthChecks("/health");
app.Run();
:::

Here's what a health check looks like:

:::C:Codeblock {lang="csharp"}
public class EmailServiceHealthCheck : IHealthCheck
{
    private readonly IOptions<EmailOptions> _options;
    
    public EmailServiceHealthCheck(IOptions<EmailOptions> options)
    {
        _options = options;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SmtpClient(_options.Value.SmtpServer, _options.Value.Port);
            await client.ConnectAsync(cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            
            return HealthCheckResult.Healthy("Email service is reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Email service is unreachable", ex);
        }
    }
}
:::

Why health checks work better:

- **Separation of concerns** - Config validation at startup. Health checks run continuously.
- **Orchestrator integration** - Kubernetes speaks health check natively.
- **Rich reporting** - Get detailed status info and custom metadata.
- **Non-blocking** - Failed health checks report degraded state. They don't prevent startup.
- **Ongoing monitoring** - Run on a schedule to catch issues that develop after deployment.

**When to use which:**

Startup validation for config that must be correct. Health checks for dependencies that might be temporarily down but shouldn't block startup. Think circuit breakers and retry policies.

## Wrapping Up

This pattern moves config validation from "runtime surprise" to "startup safety." FluentValidation plus `ValidateOnStart()` catches broken config before any requests arrive.

It's especially useful in containerized environments. Fast failures enable automated recovery. But it works just as well in traditional deployments. Clear early errors beat mysterious runtime failures.

Think about your specific needs. Strict validation helps most apps. But it's not universal. Apply it where it makes sense. Skip it where it doesn't.
