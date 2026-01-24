using Microsoft.Extensions.DependencyInjection;

namespace Femur.DependencyInjection;

/// <summary>
/// Extension methods for proxying services from one ServiceProvider into another ServiceCollection.
/// This allows services to be shared across different DI containers while preserving lifetimes and handling edge cases.
/// </summary>
public static class ProxiedServiceCollectionExtensions
{
    /// <summary>
    /// Adds services from a source ServiceCollection to the target ServiceCollection,
    /// creating factory delegates that resolve from the provided ServiceProvider.
    /// This ensures instances are shared and lifetimes are preserved.
    /// </summary>
    /// <param name="targetServiceCollection">The target service collection to register services into</param>
    /// <param name="sourceDescriptors">The source service descriptors to proxy</param>
    /// <param name="sourceServiceProvider">The service provider to resolve instances from</param>
    /// <param name="options">Optional configuration for controlling proxying behavior</param>
    /// <returns>The target service collection with proxied services registered</returns>
    public static IServiceCollection AddProxiedServices(
        this IServiceCollection targetServiceCollection,
        IEnumerable<ServiceDescriptor> sourceDescriptors,
        IServiceProvider sourceServiceProvider,
        ProxyOptions? options = null)
    {
        options ??= ProxyOptions.Default;

        foreach (var descriptor in sourceDescriptors)
        {
            // Skip services that match the filter
            if (options.ShouldSkipService != null && options.ShouldSkipService(descriptor))
            {
                continue;
            }

            // Case 1: Open generic types (e.g., IOptions<>) must preserve implementation type
            // They cannot be registered using factory delegates
            if (descriptor.ServiceType.IsGenericTypeDefinition ||
                descriptor.ImplementationType?.IsGenericTypeDefinition == true)
            {
                targetServiceCollection.Add(descriptor);
            }
            // Case 2: Singleton instances should be registered directly
            // These are already constructed objects that should be reused as-is
            else if (descriptor.ImplementationInstance != null)
            {
                targetServiceCollection.Add(descriptor);
            }
            // Case 3: Existing factories - proxy by default to share instances
            // For singletons, this ensures the same instance is returned from both providers
            else if (descriptor.ImplementationFactory != null)
            {
                if (options.PreserveExistingFactories)
                {
                    // Preserve the original factory as-is (will create new instances)
                    targetServiceCollection.Add(descriptor);
                }
                else
                {
                    // Default: resolve from source provider to share instances
                    // This ensures singletons are shared and lifetimes are preserved
                    var factory = CreateResolverFactory(descriptor.ServiceType, sourceServiceProvider);

                    var newDescriptor = new ServiceDescriptor(
                        descriptor.ServiceType,
                        factory,
                        descriptor.Lifetime);

                    targetServiceCollection.Add(newDescriptor);
                }
            }
            // Case 4: Regular closed types can use proxy factory
            else
            {
                var factory = CreateResolverFactory(descriptor.ServiceType, sourceServiceProvider);

                var newDescriptor = new ServiceDescriptor(
                    descriptor.ServiceType,
                    factory,
                    descriptor.Lifetime);

                targetServiceCollection.Add(newDescriptor);
            }
        }

        return targetServiceCollection;
    }

    /// <summary>
    /// Creates a factory function that resolves a service from the source provider.
    /// The closure captures the source provider and service type.
    /// </summary>
    /// <param name="serviceType">The service type to resolve</param>
    /// <param name="sourceProvider">The source provider to resolve from</param>
    /// <returns>A factory function that resolves the service</returns>
    private static Func<IServiceProvider, object> CreateResolverFactory(Type serviceType, IServiceProvider sourceProvider)
    {
        return _ => sourceProvider.GetRequiredService(serviceType);
    }
}

/// <summary>
/// Options for controlling service proxying behavior
/// </summary>
public class ProxyOptions
{
    /// <summary>
    /// Default proxy options
    /// </summary>
    public static ProxyOptions Default { get; } = new ProxyOptions();

    /// <summary>
    /// Optional predicate to filter out services that should not be proxied.
    /// Return true to skip the service, false to include it.
    /// </summary>
    public Func<ServiceDescriptor, bool>? ShouldSkipService { get; set; }

    /// <summary>
    /// Whether to preserve existing factory functions as-is (copying them to the target collection).
    /// If false (default), factories are proxied to resolve from the source provider, ensuring instances are shared.
    /// Set to true if you want factories to execute independently in the target provider.
    /// </summary>
    public bool PreserveExistingFactories { get; set; }
}
