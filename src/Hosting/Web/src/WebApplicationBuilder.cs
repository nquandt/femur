using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Femur.Hosting.Web;

/// <summary>
/// Provides a safe wrapper for ASP.NET Core Web applications with comprehensive error handling,
/// graceful startup/shutdown, and detailed logging throughout the application lifecycle.
/// This is created when ConfigureApplication is called on a ApplicationBuilder.
/// </summary>
public class WebApplicationBuilder
{
    private readonly ApplicationBuilder _consoleBuilder;
    private readonly Func<WebApplication, Task> _configurePipeline;

    internal WebApplicationBuilder(ApplicationBuilder consoleBuilder, Func<WebApplication, Task> configurePipeline)
    {
        this._consoleBuilder = consoleBuilder;
        this._configurePipeline = configurePipeline;
    }

    /// <summary>
    /// Builds and runs the web application with comprehensive error handling throughout the lifecycle.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the exit code:
    /// <list type="bullet">
    /// <item><description><see cref="Femur.Core.Hosting.ExitCodes.Success"/> - Normal successful completion</description></item>
    /// <item><description><see cref="Femur.Core.Hosting.ExitCodes.BuilderCreationFailed"/> - Builder creation failed</description></item>
    /// <item><description><see cref="Femur.Core.Hosting.ExitCodes.BuildFailed"/> - Application build failed</description></item>
    /// <item><description><see cref="Femur.Core.Hosting.ExitCodes.RuntimeError"/> - General runtime error</description></item>
    /// <item><description><see cref="Femur.Core.Hosting.ExitCodes.PreStartupError"/> - Pre-startup error (during app.RunAsync())</description></item>
    /// <item><description><see cref="Femur.Core.Hosting.ExitCodes.PostShutdownError"/> - Post-shutdown error</description></item>
    /// <item><description><see cref="Femur.Core.Hosting.ExitCodes.BootstrapLoggerFailed"/> - Bootstrap logger creation failed (early exit)</description></item>
    /// </list>
    /// </returns>
    public async Task<int> RunAsync()
    {
        // Access the internal state of the console builder to run as web application
        var result = await this.RunAsWebApplicationAsync();
        Environment.ExitCode = result;
        return result;
    }

    private async Task<int> RunAsWebApplicationAsync()
    {
        ILogger? bootstrapLogger = null;

        // Try to create bootstrap logger, exit early with special code if it fails
        try
        {
            bootstrapLogger = this._consoleBuilder.CreateBootstrapLogger(this._consoleBuilder.BootstrapLoggingConfig);
        }
        catch (Exception ex)
        {
            // Bootstrap logger creation failed - use temp logger and exit early
            var tempLogger = ApplicationBuilder.CreateTemporaryLogger();
            await ApplicationBuilder.HandleError(ex, null, Femur.Hosting.ExitCodes.Messages.BootstrapLoggerFailed, tempLogger);
            var exitCode = Femur.Hosting.ExitCodes.BootstrapLoggerFailed;
            Environment.ExitCode = exitCode;
            return exitCode;
        }

        Microsoft.AspNetCore.Builder.WebApplicationBuilder? webBuilder = null;
        WebApplication? app = null;

        try
        {
            bootstrapLogger.LogInformation("Creating web application builder");
            webBuilder = WebApplication.CreateBuilder(this._consoleBuilder.Args);

            if (this._consoleBuilder.ConfigureConfigurationFunc != null)
            {
                bootstrapLogger.LogInformation("Configuring web application configuration");
                await this._consoleBuilder.ConfigureConfigurationFunc(webBuilder.Configuration);
            }

            if (this._consoleBuilder.ConfigureServicesFunc != null)
            {
                bootstrapLogger.LogInformation("Configuring web application services");
                await this._consoleBuilder.ConfigureServicesFunc(webBuilder.Services);
            }

            // Configure logging in the application container using the same configuration as bootstrap
            webBuilder.Logging.ClearProviders();
            this._consoleBuilder.BootstrapLoggingConfig(webBuilder.Logging);

            bootstrapLogger.LogInformation("Building web application");
            app = webBuilder.Build();

            if (this._configurePipeline != null)
            {
                bootstrapLogger.LogInformation("Configuring web application pipeline");
                await this._configurePipeline(app);
            }

            // Pre-startup error handling
            try
            {
                bootstrapLogger.LogInformation("Starting web application");
                await app.RunAsync();
            }
            catch (Exception ex)
            {
                await ApplicationBuilder.HandleError(ex, this._consoleBuilder.PreStartupErrorHandler,
                    Femur.Hosting.ExitCodes.Messages.WebPreStartupError, GetLoggerFromApp(app) ?? bootstrapLogger);
                return Femur.Hosting.ExitCodes.PreStartupError;
            }

            // Post-shutdown handling (this code runs after app.RunAsync() completes normally)
            try
            {
                bootstrapLogger.LogInformation(Femur.Hosting.ExitCodes.Messages.SuccessfulCompletion);
            }
            catch (Exception ex)
            {
                await ApplicationBuilder.HandleError(ex, this._consoleBuilder.PostShutdownErrorHandler,
                    Femur.Hosting.ExitCodes.Messages.WebPostShutdownError, GetLoggerFromApp(app) ?? bootstrapLogger);
                return Femur.Hosting.ExitCodes.PostShutdownError;
            }

            return Femur.Hosting.ExitCodes.Success;
        }
        catch (Exception ex) when (webBuilder == null)
        {
            await ApplicationBuilder.HandleError(ex, this._consoleBuilder.BuilderErrorHandler,
                Femur.Hosting.ExitCodes.Messages.WebBuilderCreationFailed, bootstrapLogger);
            return Femur.Hosting.ExitCodes.BuilderCreationFailed;
        }
        catch (Exception ex) when (app == null)
        {
            await ApplicationBuilder.HandleError(ex, this._consoleBuilder.BuildErrorHandler,
                Femur.Hosting.ExitCodes.Messages.WebBuildFailed, bootstrapLogger);
            return Femur.Hosting.ExitCodes.BuildFailed;
        }
        catch (Exception ex)
        {
            await ApplicationBuilder.HandleError(ex, this._consoleBuilder.RuntimeErrorHandler,
                Femur.Hosting.ExitCodes.Messages.WebRuntimeError, GetLoggerFromApp(app) ?? bootstrapLogger);
            return Femur.Hosting.ExitCodes.RuntimeError;
        }
        finally
        {
            if (app != null)
            {
                try
                {
                    await app.DisposeAsync();
                }
                catch (Exception ex)
                {
                    await ApplicationBuilder.HandleError(ex, this._consoleBuilder.PostShutdownErrorHandler,
                        "Error during web application disposal", GetLoggerFromApp(app) ?? bootstrapLogger);
                }
            }

            if (bootstrapLogger is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Attempts to get a logger instance from the WebApplication's service provider.
    /// </summary>
    /// <param name="app">The WebApplication to get the logger from.</param>
    /// <returns>A logger instance if available, otherwise null.</returns>
    private static ILogger? GetLoggerFromApp(WebApplication? app)
    {
        try
        {
            if (app?.Services == null)
            {
                return null;
            }

            // Use a generic logger factory to create a logger with the discovered program type
            var loggerFactory = app.Services.GetService<ILoggerFactory>();
            if (loggerFactory != null)
            {
                // Get the category name from FemurApplicationBuilder's discovery logic
                var categoryName = ApplicationBuilder.GetLoggerCategoryName();
                return loggerFactory.CreateLogger(categoryName);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}