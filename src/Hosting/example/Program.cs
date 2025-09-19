

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Femur.Hosting.Example;

class Program
{
    static async Task<int> Main(string[] args) =>
        await ApplicationBuilder.Create(args)
            .UseDefaultConsoleLogging()
            .ConfigureConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<IGreetingService, GreetingService>();
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
        _logger = logger;
        _greetingService = greetingService;
        _config = config;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Simple console application started");

        // Get name from configuration or use default
        var name = _config["Name"] ?? "World";

        // Use the greeting service
        var greeting = _greetingService.GetGreeting(name);
        _logger.LogInformation(greeting);

        // Simulate some work
        for (int i = 1; i <= 3; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Application was cancelled at step {step}", i);
                return 1; // Return non-zero exit code for cancellation
            }

            _logger.LogInformation("Processing step {step}", i);

            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Task was cancelled during delay at step {step}", i);
                return 1; // Return non-zero exit code for cancellation
            }
        }

        _logger.LogInformation("Simple console application completed successfully");
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
        _logger = logger;
    }

    public string GetGreeting(string name)
    {
        _logger.LogDebug("Creating greeting for {name}", name);
        return $"Hello, {name}! Welcome to the simplified console app.";
    }
}