using Microsoft.Extensions.DependencyInjection;

namespace Femur.DependencyInjection;

/// <summary>
/// Provides access to the source provider for proxy types.
/// </summary>
public sealed class SourceProviderAccessor
{
    private readonly IServiceProvider _sourceProvider;
    private readonly ScopeTracker _scopeTracker;

    internal SourceProviderAccessor(IServiceProvider sourceProvider, ScopeTracker scopeTracker)
    {
        this._sourceProvider = sourceProvider;
        this._scopeTracker = scopeTracker;
    }

    /// <summary>
    /// Gets a service from the source provider root (for singletons).
    /// </summary>
    public T GetRequiredService<T>() where T : notnull
        => this._sourceProvider.GetRequiredService<T>();

    /// <summary>
    /// Gets a service from the source provider root by type.
    /// </summary>
    public object GetRequiredService(Type serviceType)
        => this._sourceProvider.GetRequiredService(serviceType);

    /// <summary>
    /// Gets a scoped service using the appropriate source scope.
    /// </summary>
    public T GetScopedService<T>(IServiceProvider currentTargetScope) where T : notnull
    {
        var scopedSource = currentTargetScope.GetService<ScopedSourceProvider>();
        var provider = scopedSource?.ServiceProvider ?? this._sourceProvider;
        return provider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a scoped service by type using the appropriate source scope.
    /// </summary>
    public object GetScopedService(Type serviceType, IServiceProvider currentTargetScope)
    {
        var scopedSource = currentTargetScope.GetService<ScopedSourceProvider>();
        var provider = scopedSource?.ServiceProvider ?? this._sourceProvider;
        return provider.GetRequiredService(serviceType);
    }
}
