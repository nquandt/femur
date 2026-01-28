using Femur.Hosting;
using Femur.Messaging;
using Femur.Messaging.Example.OrderShippedProcessor;
using Femur.Messaging.InMemory;
using Femur.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// ============================================================================
// OrderShipped Processor Microservice Example
// ============================================================================
//
// This example demonstrates how to build a production-ready message processor
// microservice using Femur.Hosting and Femur.Messaging.ServiceBus.
//
// Key patterns demonstrated:
// - Femur.Hosting's ApplicationBuilder for structured lifecycle management
// - Bootstrap logging before host initialization
// - Configuration-driven ServiceBus connection
// - Message handler with dependency injection
// - Comprehensive error handling with proper exit codes
// - Dead-lettering for permanent failures vs. retry for transient failures
//
// ============================================================================

return await ApplicationBuilder.Create(args)
    // === BOOTSTRAP LOGGING ===
    // Configure logging before the host is initialized.
    // This enables early logging during configuration and service setup.
    .UseDefaultConsoleLogging()

    // === CONFIGURATION ===
    // Load configuration from appsettings.json and environment variables.
    .ConfigureConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true);
        config.AddEnvironmentVariables();
    })

    // === SERVICE REGISTRATION ===
    .ConfigureServices(services =>
    {
        // Register email service and its options
        // Proper DI Pattern: Configure with dependency injection - IConfiguration is resolved at runtime
        services.AddOptions<MockEmailServiceOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection("EmailService").Bind(options);
            });
        services.AddSingleton<IEmailService, MockEmailService>();

        // === MESSAGING TRANSPORT CONFIGURATION ===
        // IMPORTANT: Transport selection must happen at registration time, not resolution time.
        // Since we can't use IConfiguration without building a temp provider (anti-pattern),
        // we use environment variables directly for transport selection.
        //
        // Proper DI Pattern: Read environment variables directly, not through IConfiguration
        var useInMemory = Environment.GetEnvironmentVariable("USE_INMEMORY_TRANSPORT")?.ToLowerInvariant() == "true"
            || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        if (useInMemory)
        {
            // In-Memory Transport: Use for local development and testing
            // This runs entirely in-process with no external dependencies
            services.AddFemurInMemory();
        }
        else
        {
            // Azure Service Bus Transport: Use for production and integration testing
            // Factory pattern: IConfiguration is resolved when the transport is created (proper DI pattern)
            services.AddFemurServiceBus(
                sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("ServiceBus")
                    ?? throw new InvalidOperationException(
                        "ServiceBus connection string not configured. " +
                        "Set ConnectionStrings:ServiceBus in appsettings.json or set USE_INMEMORY_TRANSPORT=true"));
        }

        // === MESSAGE HANDLER REGISTRATION ===
        // Register the OrderShipped message handler with processing options
        services.AddMessageHandler<OrderShippedMessage, OrderShippedHandler>()
            .Configure(options =>
            {
                // Maximum number of delivery attempts before dead-lettering
                // After this many retries, messages with transient errors will be dead-lettered
                options.MaxDeliveryCount = 5;

                // Enable lock tracking to cancel processing if the message lock expires
                // This prevents wasted work when processing exceeds the lock duration
                options.EnableLockTracking = true;

                // Maximum duration for message lock (0 = use Service Bus default, typically 30-60 seconds)
                options.MaxLockDuration = TimeSpan.Zero;
            });
    })

    // Skip error handler configuration - use default error handling
    // ApplicationBuilder will automatically log errors and return appropriate exit codes
    .SkipConfigureErrorHandlers()

    // === RUN APPLICATION ===
    // Start the application host and run the message processor as a hosted service
    // The MessageProcessorHostedService was automatically registered by AddMessageHandler
    // This will run until Ctrl+C or the process is terminated
    .RunAsync();

// ============================================================================
// Exit Codes:
// - 0: Success / Normal shutdown
// - 1: Builder creation failed
// - 2: Build failed (configuration or service registration error)
// - 3: Pre-startup error (during host.RunAsync() initialization)
// - 4: Runtime error (unhandled exception during message processing)
// - 5: Post-shutdown error (error during graceful shutdown)
// ============================================================================
