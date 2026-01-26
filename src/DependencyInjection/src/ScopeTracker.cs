using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace Femur.DependencyInjection;

/// <summary>
/// Tracks scope relationships between target and source providers.
/// </summary>
internal sealed class ScopeTracker : IDisposable
{
    private readonly IServiceProvider _sourceRootProvider;
    private readonly ConditionalWeakTable<IServiceProvider, ScopedSourceProvider> _scopeMap = new();
    private readonly object _lock = new();
    private bool _disposed;

    public ScopeTracker(IServiceProvider sourceRootProvider)
    {
        this._sourceRootProvider = sourceRootProvider;
    }

    public ScopedSourceProvider GetOrCreateScopedProvider(IServiceProvider targetScopeProvider)
    {
#if NETSTANDARD2_0
        if (this._disposed)
        {
            throw new ObjectDisposedException(nameof(ScopeTracker));
        }
#else
        ObjectDisposedException.ThrowIf(this._disposed, this);
#endif

        if (this._scopeMap.TryGetValue(targetScopeProvider, out var existing))
        {
            return existing;
        }

        lock (this._lock)
        {
            if (this._scopeMap.TryGetValue(targetScopeProvider, out existing))
            {
                return existing;
            }

            var sourceScope = this._sourceRootProvider.CreateScope();
            var scopedProvider = new ScopedSourceProvider(sourceScope);
            this._scopeMap.Add(targetScopeProvider, scopedProvider);
            return scopedProvider;
        }
    }

    public void Dispose()
    {
        this._disposed = true;
    }
}
