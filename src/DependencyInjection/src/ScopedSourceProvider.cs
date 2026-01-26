using Microsoft.Extensions.DependencyInjection;

namespace Femur.DependencyInjection;

/// <summary>
/// Wraps a source scope, disposed when the corresponding target scope is disposed.
/// </summary>
internal sealed class ScopedSourceProvider : IDisposable
{
    private readonly IServiceScope _sourceScope;
    private bool _disposed;

    public ScopedSourceProvider(IServiceScope sourceScope)
    {
        this._sourceScope = sourceScope;
    }

    public IServiceProvider ServiceProvider => this._sourceScope.ServiceProvider;

    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;
        this._sourceScope.Dispose();
    }
}
