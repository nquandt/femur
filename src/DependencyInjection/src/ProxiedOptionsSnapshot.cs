using Microsoft.Extensions.Options;

namespace Femur.DependencyInjection;

/// <summary>
/// Proxied IOptionsSnapshot&lt;T&gt; that resolves from the source provider's scope.
/// </summary>
public sealed class ProxiedOptionsSnapshot<T> : IOptionsSnapshot<T> where T : class, new()
{
    private readonly IOptionsSnapshot<T> _inner;

    public ProxiedOptionsSnapshot(SourceProviderAccessor accessor, IServiceProvider scopeProvider)
    {
        this._inner = accessor.GetScopedService<IOptionsSnapshot<T>>(scopeProvider);
    }

    public T Value => this._inner.Value;
    public T Get(string? name) => this._inner.Get(name);
}
