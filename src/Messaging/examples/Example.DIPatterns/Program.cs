// This file demonstrates various patterns for configuring transports using DI resolution.

using Femur.Messaging;
using Femur.Messaging.Example.DIPatterns;
using Femur.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// ============================================================================
// PATTERN 1: Connection String from IConfiguration (RECOMMENDED)
// ============================================================================
// This is the simplest and most flexible approach. Connection strings can come from:
// - appsettings.json
// - Environment variables
// - User secrets (for local dev)
// - Azure Key Vault
// - Any other configuration provider

builder.Services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("ServiceBus connection string not configured"));

// appsettings.json:
// {
//   "ConnectionStrings": {
//     "ServiceBus": "Endpoint=sb://..."
//   }
// }


// ============================================================================
// PATTERN 2: Using IOptions Pattern
// ============================================================================
// Use this when you have complex configuration that needs strong typing

/* Commented out - for demonstration only
// Configure options
builder.Services.Configure<ServiceBusConfig>(
    builder.Configuration.GetSection("ServiceBus"));

// Use options in transport registration
builder.Services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IOptions<ServiceBusConfig>>().Value.ConnectionString,
    configure: options =>
    {
        var config = builder.Configuration.GetSection("ServiceBus").Get<ServiceBusConfig>();
        options.QueueName = config!.QueueName;
    });
*/

// appsettings.json:
// {
//   "ServiceBus": {
//     "ConnectionString": "Endpoint=sb://...",
//     "QueueName": "orders"
//   }
// }


// ============================================================================
// PATTERN 3: Named Options for Multiple Environments
// ============================================================================
// Use when you need different configurations for different scenarios

builder.Services.Configure<ServiceBusConfig>("Primary",
    builder.Configuration.GetSection("ServiceBus:Primary"));
builder.Services.Configure<ServiceBusConfig>("Secondary",
    builder.Configuration.GetSection("ServiceBus:Secondary"));

// Primary transport
builder.Services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IOptionsMonitor<ServiceBusConfig>>()
        .Get("Primary").ConnectionString,
    transportKey: "primary");

// Secondary transport
builder.Services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IOptionsMonitor<ServiceBusConfig>>()
        .Get("Secondary").ConnectionString,
    transportKey: "secondary");

// Use specific transport for specific message types
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>()
    .UseTransport("primary");

builder.Services.AddMessageHandler<NotificationMessage, NotificationMessageHandler>()
    .UseTransport("secondary");


// ============================================================================
// PATTERN 4: Environment-based Configuration
// ============================================================================
// Choose configuration based on environment at runtime
builder.Services.AddFemurServiceBus(
    sp =>
    {
        var env = sp.GetRequiredService<IHostEnvironment>();
        var config = sp.GetRequiredService<IConfiguration>();
        return env.IsDevelopment()
            ? config.GetConnectionString("ServiceBus:Dev")!
            : config.GetConnectionString("ServiceBus:Prod")!;
    });

// ============================================================================
// PATTERN 5: Custom Service Resolution
// ============================================================================
// Access any registered service to determine configuration
builder.Services.AddSingleton<IConnectionStringProvider, MyConnectionStringProvider>();
builder.Services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IConnectionStringProvider>()
        .GetConnectionString("ServiceBus"));

// ============================================================================
// Register message handlers
// ============================================================================
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>();

var host = builder.Build();
await host.RunAsync();
