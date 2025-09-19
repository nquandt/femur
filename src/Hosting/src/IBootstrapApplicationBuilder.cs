using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Femur.Hosting;

/// <summary>
/// Bootstrap logging builder interface that allows configuration setup after logging.
/// </summary>
public interface IBootstrapApplicationBuilder
{
    /// <summary>
    /// Configures the application's configuration sources.
    /// This method can only be called once and comes after bootstrap logging.
    /// </summary>
    IConfigurationApplicationBuilder ConfigureConfiguration(Func<IConfigurationBuilder, Task> configure);

    /// <summary>
    /// Configures the application's configuration sources (synchronous version).
    /// This method can only be called once and comes after bootstrap logging.
    /// </summary>
    IConfigurationApplicationBuilder ConfigureConfiguration(Action<IConfigurationBuilder> configure);

    /// <summary>
    /// Skips configuration setup and proceeds to service configuration.
    /// </summary>
    IServicesApplicationBuilder SkipConfiguration();
}