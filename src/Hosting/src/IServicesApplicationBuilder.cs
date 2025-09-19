using Microsoft.Extensions.Logging;

namespace Femur.Hosting;

/// <summary>
/// Services builder interface that allows error handler setup after services.
/// </summary>
public interface IServicesApplicationBuilder
{
    /// <summary>
    /// Configures error handling for builder creation failures.
    /// </summary>
    IServicesApplicationBuilder OnBuilderError(Func<ILogger, Exception, Task> handler);

    /// <summary>
    /// Configures error handling for application build failures.
    /// </summary>
    IServicesApplicationBuilder OnBuildError(Func<ILogger, Exception, Task> handler);

    /// <summary>
    /// Configures error handling for runtime errors.
    /// </summary>
    IServicesApplicationBuilder OnRuntimeError(Func<ILogger, Exception, Task> handler);

    /// <summary>
    /// Configures error handling for pre-startup errors.
    /// </summary>
    IServicesApplicationBuilder OnPreStartupError(Func<ILogger, Exception, Task> handler);

    /// <summary>
    /// Configures error handling for post-shutdown errors.
    /// </summary>
    IServicesApplicationBuilder OnPostShutdownError(Func<ILogger, Exception, Task> handler);

    /// <summary>
    /// Proceeds to the executable builder stage without adding error handlers.
    /// </summary>
    IExecutableApplicationBuilder SkipConfigureErrorHandlers();
}