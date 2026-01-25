

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Femur.Hosting.Example;

internal sealed class Program
{
    private static async Task<int> Main(string[] args) =>
        await ApplicationBuilder.Create(args)
            .UseDefaultConsoleLogging()
            .ConfigureConfiguration(config =>
            {
                _ = config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                _ = config.AddEnvironmentVariables();
            })
            .ConfigureServices(services =>
            {
                _ = services.AddSingleton<IGreetingService, GreetingService>();
            })
            .SkipConfigureErrorHandlers()
            .RunAsync<SimpleConsoleService>();
}

/// <summary>
/// The main console application service that handles the application's logic.
/// </summary>
public class SimpleConsoleService : IConsoleApplication
{
    private readonly ILogger<SimpleConsoleService> _logger;
    private readonly IGreetingService _greetingService;
    private readonly IConfiguration _config;

    public SimpleConsoleService(
        ILogger<SimpleConsoleService> logger,
        IGreetingService greetingService,
        IConfiguration config)
    {
        this._logger = logger;
        this._greetingService = greetingService;
        this._config = config;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Simple console application started");

        // Get name from configuration or use default
        var name = this._config["Name"] ?? "World";

        // Use the greeting service
        var greeting = this._greetingService.GetGreeting(name);
        this._logger.LogInformation("{Greeting}", greeting);

        // Simulate some work
        for (var i = 1; i <= 3; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                this._logger.LogWarning("Application was cancelled at step {Step}", i);
                return 1; // Return non-zero exit code for cancellation
            }

            this._logger.LogInformation("Processing step {Step}", i);

            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                this._logger.LogWarning("Task was cancelled during delay at step {Step}", i);
                return 1; // Return non-zero exit code for cancellation
            }
        }

        this._logger.LogInformation("Simple console application completed successfully");
        return 0; // Return 0 for successful completion
    }
}

public interface IGreetingService
{
    string GetGreeting(string name);
}

public class GreetingService : IGreetingService
{
    private readonly ILogger<GreetingService> _logger;

    public GreetingService(ILogger<GreetingService> logger)
    {
        this._logger = logger;
    }

    public string GetGreeting(string name)
    {
        this._logger.LogDebug("Creating greeting for {Name}", name);
        return $"Hello, {name}! Welcome to the simplified console app.";
    }
}