using Femur.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Femur.Logging.Bootstrap;

public static class BootstrappedLoggingExtensions
{
    /// <summary>
    /// Inject all original services into the new collection, using factory delegates to resolve from the original provider.
    /// This ensures instances are shared and lifetimes are preserved.
    /// </summary>
    /// <param name="targetServiceCollection">The target service collection to register services into</param>
    /// <param name="bootstrapLogger">The bootstrapped service collection containing registrations to transfer</param>
    /// <returns>The target service collection with bootstrapped services registered</returns>
    public static IServiceCollection AddBootstrappedLogging(this IServiceCollection targetServiceCollection, BootstrapLogger bootstrapLogger)
    {
        targetServiceCollection.RemoveAll<ILoggerProvider>();

        // Proxy all services from the bootstrap container to the target container
        // The bootstrap logger should be disposed by the consumer (typically using a 'using' statement)
        // The proxied services will continue to resolve from the bootstrap ServiceProvider
        targetServiceCollection.AddProxiedServices(
            bootstrapLogger.BootstrappedServices,
            bootstrapLogger.ServiceProvider);

        return targetServiceCollection;
    }
}