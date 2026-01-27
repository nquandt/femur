namespace Femur.DependencyInjection;

/// <summary>
/// Internal marker type used to detect whether we're resolving from the root provider.
/// This service is registered as a singleton and captures the root provider instance.
/// </summary>
internal sealed class RootProviderMarker
{
    public IServiceProvider RootProvider { get; }

    public RootProviderMarker(IServiceProvider rootProvider)
    {
        this.RootProvider = rootProvider;
    }
}
