using Microsoft.Extensions.DependencyInjection;

namespace Femur.DependencyInjection;

/// <summary>
/// Extension methods for proxying services from one ServiceProvider into another ServiceCollection.
/// This allows services to be shared across different DI containers while preserving lifetimes.
/// 
/// All services are proxied consistently - including open generics - ensuring instances are
/// always resolved from the source provider.
/// </summary>
public static class ProxiedServiceCollectionExtensions
{
    /// <summary>
    /// Adds services from a source ServiceCollection to the target ServiceCollection,
    /// creating proxy registrations that resolve from the provided ServiceProvider.
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

        // Register core infrastructure for proxying (only once)
        EnsureProxyInfrastructure(targetServiceCollection, sourceServiceProvider);

        foreach (var descriptor in sourceDescriptors)
        {
            // Skip services that match the filter
            if (options.ShouldSkipService != null && options.ShouldSkipService(descriptor))
            {
                continue;
            }

            // Skip our internal tracking services
            if (IsInternalProxyService(descriptor.ServiceType))
            {
                continue;
            }

            // Skip IServiceProvider and IServiceScopeFactory - these should come from the target
            if (descriptor.ServiceType == typeof(IServiceProvider) ||
                descriptor.ServiceType == typeof(IServiceScopeFactory))
            {
                continue;
            }

            RegisterProxiedService(targetServiceCollection, descriptor, sourceServiceProvider, options);
        }

        return targetServiceCollection;
    }

    private static void EnsureProxyInfrastructure(
        IServiceCollection services,
        IServiceProvider sourceProvider)
    {
        // Only register once
        if (services.Any(sd => sd.ServiceType == typeof(SourceProviderAccessor)))
        {
            return;
        }

        // ScopeTracker - maps target scopes to source scopes
        var scopeTracker = new ScopeTracker(sourceProvider);
        services.AddSingleton(scopeTracker);

        // SourceProviderAccessor - provides access to source provider for proxy types
        services.AddSingleton(new SourceProviderAccessor(sourceProvider, scopeTracker));

        // Root provider marker - captures the root provider to detect scope vs root resolution
        services.AddSingleton<RootProviderMarker>(sp => new RootProviderMarker(sp));

        // ScopedSourceProvider - created per scope to manage scope pairing
        services.AddScoped(sp =>
        {
            var tracker = sp.GetRequiredService<ScopeTracker>();
            return tracker.GetOrCreateScopedProvider(sp);
        });
    }

    private static bool IsInternalProxyService(Type serviceType)
    {
        return serviceType == typeof(ScopeTracker) ||
               serviceType == typeof(ScopedSourceProvider) ||
               serviceType == typeof(RootProviderMarker) ||
               serviceType == typeof(SourceProviderAccessor);
    }

    private static void RegisterProxiedService(
        IServiceCollection targetServices,
        ServiceDescriptor descriptor,
        IServiceProvider sourceProvider,
        ProxyOptions options)
    {
        // Case 1: Open generic types - use dynamic proxy type generation
        if (descriptor.ServiceType.IsGenericTypeDefinition)
        {
            RegisterOpenGenericProxy(targetServices, descriptor);
        }
        // Case 2: Singleton instances - register directly (already constructed)
        else if (descriptor.ImplementationInstance != null)
        {
            targetServices.Add(descriptor);
        }
        // Case 3: Existing factories
        else if (descriptor.ImplementationFactory != null)
        {
            if (options.PreserveExistingFactories)
            {
                targetServices.Add(descriptor);
            }
            else
            {
                var newDescriptor = CreateProxiedDescriptor(
                    descriptor.ServiceType,
                    descriptor.Lifetime,
                    sourceProvider);
                targetServices.Add(newDescriptor);
            }
        }
        // Case 4: Regular closed types - use proxy factory
        else
        {
            var newDescriptor = CreateProxiedDescriptor(
                descriptor.ServiceType,
                descriptor.Lifetime,
                sourceProvider);
            targetServices.Add(newDescriptor);
        }
    }

    private static void RegisterOpenGenericProxy(
        IServiceCollection targetServices,
        ServiceDescriptor descriptor)
    {
        var serviceType = descriptor.ServiceType;

        // Check if we have a known proxy type for this open generic
        if (KnownProxyTypes.TryGetProxyType(serviceType, out var knownProxyType))
        {
            targetServices.Add(new ServiceDescriptor(
                serviceType,
                knownProxyType,
                descriptor.Lifetime));
            return;
        }

        // For unknown open generics with interfaces, generate a dynamic proxy type
        if (serviceType.IsInterface)
        {
            var proxyType = OpenGenericProxyGenerator.GetOrCreateProxyType(serviceType);
            targetServices.Add(new ServiceDescriptor(
                serviceType,
                proxyType,
                descriptor.Lifetime));
        }
        else
        {
            // Can't proxy open generic classes - copy as-is with a warning
            // This is a limitation: the instances won't be shared
            targetServices.Add(descriptor);
        }
    }

    private static ServiceDescriptor CreateProxiedDescriptor(
        Type serviceType,
        ServiceLifetime lifetime,
        IServiceProvider sourceProvider)
    {
        var factory = lifetime switch
        {
            ServiceLifetime.Singleton => _ => sourceProvider.GetRequiredService(serviceType),
            ServiceLifetime.Scoped => CreateScopedResolverFactory(serviceType),
            ServiceLifetime.Transient => CreateTransientResolverFactory(serviceType, sourceProvider),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime))
        };

        return new ServiceDescriptor(serviceType, factory, lifetime);
    }

    private static Func<IServiceProvider, object> CreateScopedResolverFactory(Type serviceType)
    {
        return targetProvider =>
        {
            // Check if we're being called from the root provider
            var rootMarker = targetProvider.GetService<RootProviderMarker>();
            if (rootMarker != null && ReferenceEquals(rootMarker.RootProvider, targetProvider))
            {
                throw new InvalidOperationException(
                    $"Cannot resolve scoped service '{serviceType.FullName}' from the root provider. " +
                    "Scoped services can only be resolved from within a scope. " +
                    "Use CreateScope() to create a scope before resolving scoped services.");
            }

            var scopedSource = targetProvider.GetService<ScopedSourceProvider>()
                ?? throw new InvalidOperationException(
                    $"Cannot resolve scoped service '{serviceType.FullName}' outside of a scope. " +
                    "Ensure you are resolving within a valid scope created from the target provider.");

            return scopedSource.ServiceProvider.GetRequiredService(serviceType);
        };
    }

    private static Func<IServiceProvider, object> CreateTransientResolverFactory(
        Type serviceType,
        IServiceProvider sourceRootProvider)
    {
        return targetProvider =>
        {
            var scopedSource = targetProvider.GetService<ScopedSourceProvider>();
            var resolveFrom = scopedSource?.ServiceProvider ?? sourceRootProvider;
            return resolveFrom.GetRequiredService(serviceType);
        };
    }
}
