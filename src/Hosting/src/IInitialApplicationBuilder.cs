using Microsoft.Extensions.Logging;

namespace Femur.Hosting;

/// <summary>
/// Initial builder interface that allows bootstrap logging configuration.
/// </summary>
public interface IInitialApplicationBuilder
{
    /// <summary>
    /// Configures bootstrap logging used during application startup.
    /// This method can only be called once.
    /// </summary>
    IBootstrapApplicationBuilder UseLogging(Action<ILoggingBuilder> configure);

    /// <summary>
    /// Uses default bootstrap logging and proceeds to configuration setup.
    /// </summary>
    IBootstrapApplicationBuilder UseDefaultConsoleLogging();
}