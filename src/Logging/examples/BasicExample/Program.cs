using Femur.Logging.Bootstrap;
using Femur.Logging.Example;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using var logger = BootstrapLogger.Create<Program>(builder =>
{
    builder.ClearProviders();
    builder.AddConsole();
});

logger.LogInformation("Starting application with bootstrapped logging.");

var builder = Host.CreateApplicationBuilder(args);

logger.LogInformation("Configuring services.");

builder.Services.AddBootstrappedLogging(logger);

// Add hosted service
builder.Services.AddHostedService<ExampleHostedService>();

var host = builder.Build();

logger.LogInformation("Starting application.");

await host.RunAsync();

logger.LogInformation("Application stopped.");
