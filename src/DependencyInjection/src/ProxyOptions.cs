using Microsoft.Extensions.DependencyInjection;

namespace Femur.DependencyInjection;

/// <summary>
/// Options for controlling service proxying behavior.
/// </summary>
public class ProxyOptions
{
    public static ProxyOptions Default { get; } = new();

    /// <summary>
    /// Optional predicate to filter out services that should not be proxied.
    /// Return true to skip the service, false to include it.
    /// </summary>
    public Func<ServiceDescriptor, bool>? ShouldSkipService { get; set; }

    /// <summary>
    /// Whether to preserve existing factory functions as-is.
    /// If false (default), factories are proxied to resolve from the source provider.
    /// </summary>
    public bool PreserveExistingFactories { get; set; }
}
