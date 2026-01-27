using Femur.Messaging;
using Femur.Messaging.Example;
using Femur.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// RECOMMENDED: Use DI resolution for connection strings
// This allows configuration to come from appsettings.json, environment variables, etc.
// and supports IOptions, named options, and configuration reloading

// 1. Add the transport using DI resolution
builder.Services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("ServiceBus connection string not configured"));

// 2. Register handlers with the fluent API
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>()
    .Configure(options =>
    {
        // Configure message processing options
        options.MaxDeliveryCount = 5;
    });

var host = builder.Build();
host.Run();
