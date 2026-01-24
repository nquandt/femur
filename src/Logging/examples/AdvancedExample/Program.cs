using System.Diagnostics;
using Femur.Logging.AdvancedExample;
using Femur.Logging.Bootstrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Constants for OpenTelemetry
const string serviceName = "AdvancedExample";
const string serviceVersion = "1.0.0";

// Create shared ResourceBuilder for all OpenTelemetry signals (logging, tracing, metrics)
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName, serviceVersion: serviceVersion)
    .AddAttributes(new Dictionary<string, object>
    {
        ["environment"] = "development",
        ["host.name"] = Environment.MachineName
    });

// Create bootstrap logger with structured logging and OpenTelemetry
// Register shared services like ActivitySource that need to be available in both containers
using var logger = BootstrapLogger.Create<Program>(
    builder =>
    {
        builder.ClearProviders();
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Debug);

        // Add OpenTelemetry logging
        builder.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;

            // Use OTLP exporter if endpoint is configured, otherwise fallback to console
            var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                options.AddOtlpExporter();
            }
            else
            {
                // Export to console for local development
                options.AddConsoleExporter();
            }
        });
    },
    services =>
    {
        // Register ActivitySource as a shared service
        // This will be available in both bootstrap and main containers
        var activitySource = new ActivitySource(serviceName, serviceVersion);
        services.AddSingleton(activitySource);
    });

logger.LogInformation("=== Application Starting ===");
logger.LogInformation("Bootstrap logger initialized successfully");

try
{
    // Perform startup validation
    logger.LogInformation("Running startup validation checks...");

    ValidateEnvironment(logger);
    ValidateConfiguration(logger);

    logger.LogInformation("Startup validation completed successfully");

    // Build the host
    var builder = Host.CreateApplicationBuilder(args);

    logger.LogInformation("Configuring services...");

    // Configure options
    builder.Services.Configure<WorkerOptions>(options =>
    {
        options.ProcessingInterval = TimeSpan.FromSeconds(3);
        options.MaxRetries = 3;
        options.EnableValidation = true;
    });

    // Add bootstrapped logging
    builder.Services.AddBootstrappedLogging(logger);

    // Add OpenTelemetry tracing and metrics using shared ResourceBuilder
    var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    var useOtlp = !string.IsNullOrEmpty(otlpEndpoint);

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .SetResourceBuilder(resourceBuilder)
                .AddSource(serviceName);

            if (useOtlp)
            {
                tracing.AddOtlpExporter();
            }
            else
            {
                tracing.AddConsoleExporter();
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .SetResourceBuilder(resourceBuilder)
                .AddRuntimeInstrumentation();

            if (useOtlp)
            {
                metrics.AddOtlpExporter();
            }
            else
            {
                metrics.AddConsoleExporter();
            }
        });

    // Register services
    // Note: ActivitySource is already registered in the bootstrap container
    builder.Services.AddSingleton<IWorkItemValidator, WorkItemValidator>();
    builder.Services.AddSingleton<IWorkItemProcessor, WorkItemProcessor>();
    builder.Services.AddHostedService<WorkerService>();
    builder.Services.AddHostedService<HealthCheckService>();

    logger.LogInformation("Service configuration completed");

    // Build the host
    logger.LogInformation("Building application host...");
    var host = builder.Build();

    // Validate dependencies after build
    logger.LogInformation("Validating service dependencies...");
    ValidateServiceDependencies(host.Services, logger);

    logger.LogInformation("=== Application Starting Successfully ===");

    // Run the application
    await host.RunAsync();

    logger.LogInformation("=== Application Stopped Gracefully ===");
}
catch (OptionsValidationException ex)
{
    logger.LogCritical(ex, "Configuration validation failed: {Message}", ex.Message);
    logger.LogError("Failures: {Failures}", string.Join(", ", ex.Failures));
    return 1;
}
catch (InvalidOperationException ex)
{
    logger.LogCritical(ex, "Startup validation failed: {Message}", ex.Message);
    return 2;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Unhandled exception during application startup or execution");
    return 99;
}
finally
{
    logger.LogInformation("Shutting down bootstrap logger...");
}

return 0;

// Startup validation methods
static void ValidateEnvironment(ILogger logger)
{
    logger.LogDebug("Validating environment...");

    // Check temp directory exists and is writable
    var tempPath = Path.GetTempPath();
    if (!Directory.Exists(tempPath))
    {
        throw new InvalidOperationException($"Temp directory does not exist: {tempPath}");
    }

    logger.LogDebug("Temp directory validated: {TempPath}", tempPath);

    // Check available disk space
    var drive = new DriveInfo(Path.GetPathRoot(tempPath)!);
    var availableSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

    logger.LogInformation("Available disk space: {Space:F2} GB", availableSpaceGB);

    if (availableSpaceGB < 0.1)
    {
        logger.LogWarning("Low disk space detected: {Space:F2} GB", availableSpaceGB);
    }
}

static void ValidateConfiguration(ILogger logger)
{
    logger.LogDebug("Validating configuration...");

    // Simulate configuration validation
    var requiredSettings = new[] { "HOME", "PATH" };

    foreach (var setting in requiredSettings)
    {
        var value = Environment.GetEnvironmentVariable(setting);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required environment variable not set: {setting}");
        }

        logger.LogDebug("Configuration validated: {Setting}", setting);
    }
}

static void ValidateServiceDependencies(IServiceProvider services, ILogger logger)
{
    logger.LogDebug("Validating service dependencies...");

    // Ensure critical services can be resolved
    var criticalServices = new[]
    {
        typeof(ActivitySource),
        typeof(IWorkItemValidator),
        typeof(IWorkItemProcessor),
        typeof(IOptions<WorkerOptions>)
    };

    foreach (var serviceType in criticalServices)
    {
        try
        {
            var service = services.GetRequiredService(serviceType);
            logger.LogDebug("Service resolved successfully: {ServiceType}", serviceType.Name);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to resolve required service: {serviceType.Name}", ex);
        }
    }

    logger.LogInformation("All service dependencies validated successfully");
}
