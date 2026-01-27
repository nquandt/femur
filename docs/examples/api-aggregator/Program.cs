using ApiAggregator;
using Femur;
using Femur.Hosting;
using Femur.Serialization;
using Microsoft.Extensions.DependencyInjection;

// Create and run the API aggregator application
// Demonstrates:
// - ApplicationBuilder with fluent lifecycle stages
// - Bootstrap logging (logs before host fully initialized)
// - IStandardOptions with FluentValidation at startup
// - IAsyncSerializer for JSON serialization
// - Error handling with exit codes

return await ApplicationBuilder.Create(args)
    // Stage 1: Bootstrap logging
    // Creates BootstrapLogger that logs configuration and service registration
    .UseDefaultConsoleLogging()

    // Stage 2: Configuration
    // Load configuration files (appsettings.json, environment-specific overrides)
    .ConfigureConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
        config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
        config.AddEnvironmentVariables();
    })

    // Stage 3: Services
    // Register services in dependency injection container
    .ConfigureServices((context, services) =>
    {
        // Register ApiAggregatorOptions with convention-based validation
        // Validation runs at startup via ValidateOnStart()
        // If configuration is invalid, application exits with ExitCodes.BuildFailed
        services.TryConfigureByConventionWithValidation<ApiAggregatorOptions>();

        // Register JSON serializer
        services.AddDefaultJsonSerializer();

        // Register HttpClient factory for making API requests
        services.AddHttpClient();
    })

    // Error handling at build phase (configuration/services registration)
    .OnBuildError((exception, logger) =>
    {
        logger.LogCritical(exception, "Failed to build application");
        return ExitCodes.BuildFailed;  // Exit code 2
    })

    // Error handling during pre-startup phase (host initialization)
    .OnPreStartupError((exception, logger) =>
    {
        logger.LogCritical(exception, "Failed to start application");
        return ExitCodes.PreStartupError;  // Exit code 4
    })

    // Error handling during runtime (unhandled exceptions)
    .OnRuntimeError((exception, logger) =>
    {
        logger.LogCritical(exception, "Runtime error occurred");
        return ExitCodes.RuntimeError;  // Exit code 3
    })

    // Stage 4: Run
    // Build and run the application with IConsoleApplication
    // AggregatorService.ExecuteAsync() runs, then application exits
    .RunAsync<AggregatorService>();
