// Example showing manual message consumption (pull-based) instead of automatic processing (push-based).

using Femur.Messaging;
using Femur.Messaging.Example.ManualConsumption;
using Femur.Messaging.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add transport
builder.Services.AddFemurInMemory();

// Register client for MANUAL consumption (not automatic processing)
builder.Services.AddMessageClient<OrderMessage>();

// Register our custom service that will consume messages manually
builder.Services.AddHostedService<ManualConsumerService>();

var host = builder.Build();
await host.RunAsync();
