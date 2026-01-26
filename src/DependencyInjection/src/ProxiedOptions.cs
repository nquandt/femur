using Microsoft.Extensions.Options;

namespace Femur.DependencyInjection;

/// <summary>
/// Proxied IOptions&lt;T&gt; that resolves from the source provider.
/// </summary>
public sealed class ProxiedOptions<T> : IOptions<T> where T : class, new()
{
    public T Value { get; }

    public ProxiedOptions(SourceProviderAccessor accessor)
    {
        this.Value = accessor.GetRequiredService<IOptions<T>>().Value;
    }
}
