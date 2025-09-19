using Microsoft.Extensions.DependencyInjection;

namespace Femur.Hosting;

/// <summary>
/// Configuration builder interface that allows service setup after configuration.
/// </summary>
public interface IConfigurationApplicationBuilder
{
    /// <summary>
    /// Configures the application's dependency injection services.
    /// This method can only be called once and must come after ConfigureConfiguration.
    /// </summary>
    IServicesApplicationBuilder ConfigureServices(Func<IServiceCollection, Task> configure);

    /// <summary>
    /// Configures the application's dependency injection services (synchronous version).
    /// This method can only be called once and must come after ConfigureConfiguration.
    /// </summary>
    IServicesApplicationBuilder ConfigureServices(Action<IServiceCollection> configure);

    /// <summary>
    /// Skips service configuration and proceeds to error handler setup.
    /// </summary>
    IServicesApplicationBuilder SkipServices();
}