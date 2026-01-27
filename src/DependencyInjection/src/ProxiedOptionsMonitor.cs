using Microsoft.Extensions.Options;

namespace Femur.DependencyInjection;

/// <summary>
/// Proxied IOptionsMonitor&lt;T&gt; that resolves from the source provider.
/// </summary>
public sealed class ProxiedOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    private readonly IOptionsMonitor<T> _inner;

    public ProxiedOptionsMonitor(SourceProviderAccessor accessor)
    {
        this._inner = accessor.GetRequiredService<IOptionsMonitor<T>>();
    }

    public T CurrentValue => this._inner.CurrentValue;
    public T Get(string? name) => this._inner.Get(name);
    public IDisposable? OnChange(Action<T, string?> listener) => this._inner.OnChange(listener);
}
