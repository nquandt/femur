using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Femur.Hosting.Web")]

namespace Femur.Hosting;

/// <summary>
/// Provides a safe wrapper for Console applications with comprehensive error handling,
/// graceful startup/shutdown, and detailed logging throughout the application lifecycle.
/// For Web applications, use the Femur.Hosting.Web extension package.
/// </summary>
public class ApplicationBuilder :
    IInitialApplicationBuilder,
    IBootstrapApplicationBuilder,
    IConfigurationApplicationBuilder,
    IServicesApplicationBuilder,
    IExecutableApplicationBuilder
{
    private readonly string[] _args;
    private Func<IServiceCollection, Task>? _configureServices;
    private Func<IConfigurationBuilder, Task>? _configureConfiguration;
    private Func<ILogger, Exception, Task>? _onBuilderError;
    private Func<ILogger, Exception, Task>? _onBuildError;
    private Func<ILogger, Exception, Task>? _onRuntimeError;
    private Func<ILogger, Exception, Task>? _onPreStartupError;
    private Func<ILogger, Exception, Task>? _onPostShutdownError;
    private Action<ILoggingBuilder> _useBootstrapLogging = builder =>
    {
        _ = builder.ClearProviders();
        _ = builder.AddConsole();
        _ = builder.SetMinimumLevel(LogLevel.Information);
    };

    // State tracking for enforcing single calls
    private bool _bootstrapLoggingConfigured;
    private bool _configurationConfigured;
    private bool _servicesConfigured;

    /// <summary>
    /// Gets the command line arguments. Internal for use by Web extensions.
    /// </summary>
    internal string[] Args => this._args;

    /// <summary>
    /// Gets the service configuration function. Internal for use by Web extensions.
    /// </summary>
    internal Func<IServiceCollection, Task>? ConfigureServicesFunc => this._configureServices;

    /// <summary>
    /// Gets the configuration setup function. Internal for use by Web extensions.
    /// </summary>
    internal Func<IConfigurationBuilder, Task>? ConfigureConfigurationFunc => this._configureConfiguration;

    /// <summary>
    /// Gets the bootstrap logging configuration. Internal for use by Web extensions.
    /// </summary>
    internal Action<ILoggingBuilder> BootstrapLoggingConfig => this._useBootstrapLogging;

    /// <summary>
    /// Gets the builder error handler. Internal for use by Web extensions.
    /// </summary>
    internal Func<ILogger, Exception, Task>? BuilderErrorHandler => this._onBuilderError;

    /// <summary>
    /// Gets the build error handler. Internal for use by Web extensions.
    /// </summary>
    internal Func<ILogger, Exception, Task>? BuildErrorHandler => this._onBuildError;

    /// <summary>
    /// Gets the runtime error handler. Internal for use by Web extensions.
    /// </summary>
    internal Func<ILogger, Exception, Task>? RuntimeErrorHandler => this._onRuntimeError;

    /// <summary>
    /// Gets the pre-startup error handler. Internal for use by Web extensions.
    /// </summary>
    internal Func<ILogger, Exception, Task>? PreStartupErrorHandler => this._onPreStartupError;

    /// <summary>
    /// Gets the post-shutdown error handler. Internal for use by Web extensions.
    /// </summary>
    internal Func<ILogger, Exception, Task>? PostShutdownErrorHandler => this._onPostShutdownError;

    private ApplicationBuilder(string[] args)
    {
        this._args = args;
    }

    /// <summary>
    /// Creates a new instance of the ApplicationBuilder with the specified command line arguments.
    /// </summary>
    /// <param name="args">The command line arguments to pass to the application.</param>
    /// <returns>A new ApplicationBuilder instance for method chaining.</returns>
    public static IInitialApplicationBuilder Create(string[] args) => new ApplicationBuilder(args);

    /// <summary>
    /// Configures bootstrap logging used during application startup.
    /// This method can only be called once.
    /// </summary>
    /// <param name="configure">An action to configure the logging builder.</param>
    /// <returns>The bootstrap builder stage for method chaining.</returns>
    public IBootstrapApplicationBuilder UseLogging(Action<ILoggingBuilder> configure)
    {
        if (this._bootstrapLoggingConfigured)
        {
            throw new InvalidOperationException("UseBootstrapLogging can only be called once.");
        }

        this._bootstrapLoggingConfigured = true;
        this._useBootstrapLogging = configure;
        return this;
    }

    /// <summary>
    /// Uses default bootstrap logging and proceeds to configuration setup.
    /// </summary>
    /// <returns>The bootstrap builder stage for method chaining.</returns>
    public IBootstrapApplicationBuilder UseDefaultConsoleLogging()
    {
        if (this._bootstrapLoggingConfigured)
        {
            throw new InvalidOperationException("Bootstrap logging can only be configured once.");
        }

        this._bootstrapLoggingConfigured = true;
        // Keep the existing default bootstrap logging configuration
        return this;
    }

    /// <summary>
    /// Configures services for dependency injection using an asynchronous configuration function.
    /// This method can only be called once and must come after configuration.
    /// </summary>
    /// <param name="configure">An asynchronous function to configure the service collection.</param>
    /// <returns>The services builder stage for method chaining.</returns>
    public IServicesApplicationBuilder ConfigureServices(Func<IServiceCollection, Task> configure)
    {
        if (this._servicesConfigured)
        {
            throw new InvalidOperationException("ConfigureServices can only be called once.");
        }

        this._servicesConfigured = true;
        this._configureServices = configure;
        return this;
    }

    /// <summary>
    /// Configures services for dependency injection using a synchronous configuration action.
    /// This method can only be called once and must come after configuration.
    /// </summary>
    /// <param name="configure">An action to configure the service collection.</param>
    /// <returns>The services builder stage for method chaining.</returns>
    public IServicesApplicationBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        if (this._servicesConfigured)
        {
            throw new InvalidOperationException("ConfigureServices can only be called once.");
        }

        this._servicesConfigured = true;
        this._configureServices = services =>
        {
            configure(services);
            return Task.CompletedTask;
        };
        return this;
    }

    /// <summary>
    /// Skips service configuration and proceeds to error handler setup.
    /// </summary>
    /// <returns>The services builder stage for method chaining.</returns>
    public IServicesApplicationBuilder SkipServices()
    {
        if (this._servicesConfigured)
        {
            throw new InvalidOperationException("Cannot skip services after they have already been configured.");
        }

        this._servicesConfigured = true;
        return this;
    }

    /// <summary>
    /// Configures the application configuration using an asynchronous configuration function.
    /// This method can only be called once.
    /// </summary>
    /// <param name="configure">An asynchronous function to configure the configuration builder.</param>
    /// <returns>The configuration builder stage for method chaining.</returns>
    public IConfigurationApplicationBuilder ConfigureConfiguration(Func<IConfigurationBuilder, Task> configure)
    {
        if (this._configurationConfigured)
        {
            throw new InvalidOperationException("ConfigureConfiguration can only be called once.");
        }

        this._configurationConfigured = true;
        this._configureConfiguration = configure;
        return this;
    }

    /// <summary>
    /// Configures the application configuration using a synchronous configuration action.
    /// This method can only be called once.
    /// </summary>
    /// <param name="configure">An action to configure the configuration builder.</param>
    /// <returns>The configuration builder stage for method chaining.</returns>
    public IConfigurationApplicationBuilder ConfigureConfiguration(Action<IConfigurationBuilder> configure)
    {
        if (this._configurationConfigured)
        {
            throw new InvalidOperationException("ConfigureConfiguration can only be called once.");
        }

        this._configurationConfigured = true;
        this._configureConfiguration = config =>
        {
            configure(config);
            return Task.CompletedTask;
        };
        return this;
    }

    /// <summary>
    /// Skips configuration setup and proceeds to service configuration.
    /// </summary>
    /// <returns>The services builder stage for method chaining.</returns>
    public IServicesApplicationBuilder SkipConfiguration()
    {
        if (this._configurationConfigured)
        {
            throw new InvalidOperationException("Cannot skip configuration after it has already been configured.");
        }

        this._configurationConfigured = true;
        return this;
    }

    /// <summary>
    /// Sets a custom error handler for application builder creation failures.
    /// </summary>
    /// <param name="handler">An asynchronous function to handle builder creation errors.</param>
    /// <returns>The services builder stage for method chaining.</returns>
    public IServicesApplicationBuilder OnBuilderError(Func<ILogger, Exception, Task> handler)
    {
        this._onBuilderError = handler;
        return this;
    }

    /// <summary>
    /// Sets a custom error handler for application build failures.
    /// </summary>
    /// <param name="handler">An asynchronous function to handle build errors.</param>
    /// <returns>The services builder stage for method chaining.</returns>
    public IServicesApplicationBuilder OnBuildError(Func<ILogger, Exception, Task> handler)
    {
        this._onBuildError = handler;
        return this;
    }

    /// <summary>
    /// Sets a custom error handler for general runtime exceptions.
    /// </summary>
    /// <param name="handler">An asynchronous function to handle runtime errors.</param>
    /// <returns>The services builder stage for method chaining.</returns>
    public IServicesApplicationBuilder OnRuntimeError(Func<ILogger, Exception, Task> handler)
    {
        this._onRuntimeError = handler;
        return this;
    }

    /// <summary>
    /// Sets a custom error handler for errors that occur during app.RunAsync() startup.
    /// </summary>
    /// <param name="handler">An asynchronous function to handle pre-startup errors.</param>
    /// <returns>The services builder stage for method chaining.</returns>
    public IServicesApplicationBuilder OnPreStartupError(Func<ILogger, Exception, Task> handler)
    {
        this._onPreStartupError = handler;
        return this;
    }

    /// <summary>
    /// Sets a custom error handler for errors during graceful shutdown and disposal.
    /// </summary>
    /// <param name="handler">An asynchronous function to handle post-shutdown errors.</param>
    /// <returns>The services builder stage for method chaining.</returns>
    public IServicesApplicationBuilder OnPostShutdownError(Func<ILogger, Exception, Task> handler)
    {
        this._onPostShutdownError = handler;
        return this;
    }

    /// <summary>
    /// Proceeds to the executable builder stage without adding error handlers.
    /// </summary>
    /// <returns>The executable builder stage for method chaining.</returns>
    public IExecutableApplicationBuilder SkipConfigureErrorHandlers()
    {
        return this;
    }

    /// <summary>
    /// Creates a bootstrap logger using the specified configuration. Internal for use by Web extensions.
    /// </summary>
    /// <param name="configure">The action to configure the logging builder.</param>
    /// <returns>A configured logger instance.</returns>
    internal BootstrapLogger CreateBootstrapLogger(Action<ILoggingBuilder> configure)
    {
        return new BootstrapLogger(configure);
    }

    /// <summary>
    /// Creates a temporary logger with default console configuration as a fallback when the primary bootstrap logger fails.
    /// Internal for use by Web extensions.
    /// </summary>
    /// <returns>A temporary logger instance, or NullLogger if creation fails.</returns>
    internal static ILogger CreateTemporaryLogger()
    {
        try
        {
            var sc = new ServiceCollection();
            _ = sc.AddLogging(builder =>
            {
                _ = builder.ClearProviders();
                _ = builder.AddConsole();
                _ = builder.SetMinimumLevel(LogLevel.Information);
            });
            var sp = sc.BuildServiceProvider();

            return sp.GetRequiredService<ILogger>();
        }
        catch
        {
            // If even the temporary logger fails, return a null logger that won't throw
            return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }
    }

    /// <summary>
    /// Builds and runs the console application with comprehensive error handling throughout the lifecycle.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the exit code:
    /// <list type="bullet">
    /// <item><description><see cref="ExitCodes.Success"/> - Normal successful completion</description></item>
    /// <item><description><see cref="ExitCodes.BuilderCreationFailed"/> - Builder creation failed</description></item>
    /// <item><description><see cref="ExitCodes.BuildFailed"/> - Application build failed</description></item>
    /// <item><description><see cref="ExitCodes.RuntimeError"/> - General runtime error</description></item>
    /// <item><description><see cref="ExitCodes.PreStartupError"/> - Pre-startup error (during host.RunAsync())</description></item>
    /// <item><description><see cref="ExitCodes.PostShutdownError"/> - Post-shutdown error</description></item>
    /// <item><description><see cref="ExitCodes.BootstrapLoggerFailed"/> - Bootstrap logger creation failed (early exit)</description></item>
    /// </list>
    /// </returns>
    public async Task<int> RunAsync()
    {
        BootstrapLogger? bootstrapLogger = null;
        // Try to create bootstrap logger, exit early with special code if it fails

        try
        {
            bootstrapLogger = this.CreateBootstrapLogger(this._useBootstrapLogging);
        }
        catch (Exception ex)
        {
            // Bootstrap logger creation failed - use temp logger and exit early
            var tempLogger = CreateTemporaryLogger();
            await HandleError(ex, null, ExitCodes.Messages.BootstrapLoggerFailed, tempLogger);
            var exitCode = ExitCodes.BootstrapLoggerFailed;
            Environment.ExitCode = exitCode;
            return exitCode;
        }

        var result = await this.RunAsConsoleApplicationAsync(bootstrapLogger);
        Environment.ExitCode = result;
        return result;
    }

    /// <summary>
    /// Builds and runs the console application with a specific IConsoleApplication implementation.
    /// The specified service type is automatically registered and will be the main execution service.
    /// </summary>
    /// <typeparam name="TApplication">The IConsoleApplication implementation to run as the main application.</typeparam>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the exit code:
    /// <list type="bullet">
    /// <item><description><see cref="ExitCodes.Success"/> - Normal successful completion</description></item>
    /// <item><description><see cref="ExitCodes.BuilderCreationFailed"/> - Builder creation failed</description></item>
    /// <item><description><see cref="ExitCodes.BuildFailed"/> - Application build failed</description></item>
    /// <item><description><see cref="ExitCodes.RuntimeError"/> - General runtime error</description></item>
    /// <item><description><see cref="ExitCodes.PreStartupError"/> - Pre-startup error (during host.RunAsync())</description></item>
    /// <item><description><see cref="ExitCodes.PostShutdownError"/> - Post-shutdown error</description></item>
    /// <item><description><see cref="ExitCodes.BootstrapLoggerFailed"/> - Bootstrap logger creation failed (early exit)</description></item>
    /// </list>
    /// </returns>
    public async Task<int> RunAsync<TApplication>() where TApplication : class, IConsoleApplication
    {
        ILogger? bootstrapLogger = null;

        // Try to create bootstrap logger, exit early with special code if it fails
        try
        {
            bootstrapLogger = this.CreateBootstrapLogger(this._useBootstrapLogging);
        }
        catch (Exception ex)
        {
            // Bootstrap logger creation failed - use temp logger and exit early
            var tempLogger = CreateTemporaryLogger();
            await HandleError(ex, null, ExitCodes.Messages.BootstrapLoggerFailed, tempLogger);
            var exitCode = ExitCodes.BootstrapLoggerFailed;
            Environment.ExitCode = exitCode;
            return exitCode;
        }

        // lets capture the _configureServices and then change it to inject the TApplication
        var previousConfigureServices = this._configureServices;
        this._configureServices = services =>
        {
            // If there was a previous configuration, apply it first
            _ = (previousConfigureServices?.Invoke(services));

            // Register the console application only if not already registered            
            services.TryAddSingleton<TApplication>();

            // Register the wrapper hosted service
            _ = services.AddSingleton<ConsoleApplicationHostedService<TApplication>>();
            _ = services.AddSingleton<IConsoleApplicationHostedService>(sp => sp.GetRequiredService<ConsoleApplicationHostedService<TApplication>>());
            _ = services.AddHostedService(provider =>
                provider.GetRequiredService<ConsoleApplicationHostedService<TApplication>>());

            return Task.CompletedTask;
        };

        var result = await this.RunAsConsoleApplicationAsync(bootstrapLogger);
        Environment.ExitCode = result;
        return result;
    }

    /// <summary>
    /// Runs the application as a Console application using the generic host.
    /// </summary>
    /// <param name="bootstrapLogger">The bootstrap logger to use for early logging.</param>
    /// <returns>Exit code indicating the result of the operation.</returns>
    private async Task<int> RunAsConsoleApplicationAsync(ILogger bootstrapLogger)
    {
        HostApplicationBuilder? hostBuilder = null;
        IHost? host = null;

        try
        {
            bootstrapLogger.LogInformation("Creating console application host builder");
            hostBuilder = Host.CreateApplicationBuilder(this._args);

            if (this._configureConfiguration != null)
            {
                bootstrapLogger.LogInformation("Configuring console application configuration");
                // Use GetAwaiter().GetResult() to synchronously wait for async operation
                await this._configureConfiguration(hostBuilder.Configuration);
            }

            if (this._configureServices != null)
            {
                bootstrapLogger.LogInformation("Configuring console application services");
                // Use GetAwaiter().GetResult() to synchronously wait for async operation
                await this._configureServices(hostBuilder.Services);
            }

            // Configure logging in the application container using the same configuration as bootstrap
            _ = hostBuilder.Logging.ClearProviders();
            this._useBootstrapLogging(hostBuilder.Logging);

            bootstrapLogger.LogInformation("Building console application host");
            host = hostBuilder.Build();

            // Pre-startup error handling
            try
            {
                bootstrapLogger.LogInformation("Starting console application");

                var consoleHostedService = host.Services.GetService<IConsoleApplicationHostedService>();

                await host.RunAsync();

                // Host has shut down gracefully, now handle any disposal errors
                bootstrapLogger.LogInformation(ExitCodes.Messages.SuccessfulCompletion);

                if (consoleHostedService != null)
                {
                    if (consoleHostedService.ExitCode != -1)
                    {
                        return consoleHostedService.ExitCode;
                    }
                }
            }
            catch (Exception ex) when (IsDisposalRelatedError(ex))
            {
                // Disposal errors should be treated as post-shutdown errors, not pre-startup errors
                await HandleError(ex, this._onPostShutdownError, ExitCodes.Messages.ConsolePostShutdownError,
                    bootstrapLogger);
                return ExitCodes.PostShutdownError;
            }
            catch (Exception ex)
            {
                await HandleError(ex, this._onPreStartupError, ExitCodes.Messages.ConsolePreStartupError,
                    bootstrapLogger);
                return ExitCodes.PreStartupError;
            }

            return ExitCodes.Success;
        }
        catch (Exception ex) when (hostBuilder == null)
        {
            await HandleError(ex, this._onBuilderError, ExitCodes.Messages.ConsoleBuilderCreationFailed, bootstrapLogger);
            return ExitCodes.BuilderCreationFailed;
        }
        catch (Exception ex) when (host == null)
        {
            await HandleError(ex, this._onBuildError, ExitCodes.Messages.ConsoleBuildFailed, bootstrapLogger);
            return ExitCodes.BuildFailed;
        }
        catch (Exception ex)
        {
            await HandleError(ex, this._onRuntimeError, ExitCodes.Messages.ConsoleRuntimeError,
                bootstrapLogger);
            return ExitCodes.RuntimeError;
        }
        finally
        {
            if (host != null)
            {
                try
                {
                    host.Dispose();
                }
                catch (Exception ex)
                {
                    await HandleError(ex, this._onPostShutdownError, "Error during console application host disposal",
                        bootstrapLogger);
                }
            }

            if (bootstrapLogger is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Determines if an exception is related to disposal/shutdown operations.
    /// </summary>
    /// <param name="ex">The exception to check.</param>
    /// <returns>True if the exception is disposal-related, false otherwise.</returns>
    private static bool IsDisposalRelatedError(Exception ex)
    {
        // Check for disposal-related exceptions
        if (ex is ObjectDisposedException)
        {
            return true;
        }

        // Check for disposal in the stack trace or message
        var message = ex.ToString();
        if (message.Contains("DisposeAsync") || message.Contains("Dispose()") ||
            message.Contains("disposed object") || message.Contains("disposal"))
        {
            return true;
        }

        // Check inner exceptions recursively
        var innerEx = ex.InnerException;
        while (innerEx != null)
        {
            if (innerEx is ObjectDisposedException)
            {
                return true;
            }

            var innerMessage = innerEx.ToString();
            if (innerMessage.Contains("DisposeAsync") || innerMessage.Contains("Dispose()") ||
                innerMessage.Contains("disposed object") || innerMessage.Contains("disposal"))
            {
                return true;
            }

            innerEx = innerEx.InnerException;
        }

        return false;
    }

    // The IsApplicationConstructorError<TApplication> helper was removed because it's not referenced anywhere.

    /// <summary>
    /// Handles errors by calling custom error handlers or providing default error logging behavior.
    /// Internal for use by Web extensions.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="customHandler">Optional custom error handler to call.</param>
    /// <param name="defaultMessage">Default message to log if no custom handler is provided.</param>
    /// <param name="logger">Logger instance to use for error logging.</param>
    /// <returns>A task that represents the asynchronous error handling operation.</returns>
    internal static async Task HandleError(
        Exception ex,
        Func<ILogger, Exception, Task>? customHandler,
        string defaultMessage,
        ILogger? logger)
    {
        if (customHandler != null && logger != null)
        {
            await customHandler(logger, ex);
        }
        else if (customHandler != null && logger == null)
        {
            // If we have a custom handler but no logger, create a minimal logger for the handler
            var tempLogger = CreateTemporaryLogger();
            await customHandler(tempLogger, ex);
        }
        else
        {
            var message = $"{defaultMessage}: {ex}";

            if (logger != null)
            {
#pragma warning disable CA2254 // Template should be a static expression
                logger.LogCritical(ex, defaultMessage);
#pragma warning restore CA2254 // Template should be a static expression
            }
            else
            {
                Console.WriteLine($"CRITICAL: {message}");
                await File.AppendAllTextAsync("critical-errors.log",
                    $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {message}\n");
            }
        }
    }
}

