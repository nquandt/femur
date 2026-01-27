// Example showing how to use multiple transports in the same application.

using Femur.Messaging;
using Femur.Messaging.Example.MultiTransport;
using Femur.Messaging.InMemory;
using Femur.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Example 1: Single transport (default behavior - no changes needed!)
// builder.Services.AddFemurServiceBus(connectionString);
// builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>();

// Example 2: Multiple transports with named keys
// Register two different transports
builder.Services.AddFemurInMemory(transportKey: "local");
builder.Services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("ServiceBus")
        ?? "Endpoint=sb://test.servicebus.windows.net/",
    transportKey: "azure");

// Route different message types to different transports
builder.Services.AddMessageHandler<OrderMessage, OrderMessageHandler>()
    .UseTransport("local");  // Orders go to in-memory queue

// If you had another message type:
// builder.Services.AddMessageHandler<PaymentMessage, PaymentMessageHandler>()
//     .UseTransport("azure");  // Payments go to Service Bus

var host = builder.Build();
host.Run();
