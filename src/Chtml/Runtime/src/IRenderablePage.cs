
namespace Femur.Chtml.Runtime;

/// <summary>
/// Interface for renderable pages with static abstract methods.
/// Pages inherit from IRenderable&lt;TInputProps, TGlobalProps&gt; and add route information for automatic registration.
/// </summary>
/// <typeparam name="TInputProps">The props type that callers pass (public API).</typeparam>
/// <typeparam name="TGlobalProps">The global props type.</typeparam>
public interface IRenderablePage<TInputProps, TGlobalProps> : IRenderable<TInputProps, TGlobalProps>
    where TInputProps : class
    where TGlobalProps : class
{
    /// <summary>
    /// The HTTP route path for this page (e.g., "/", "/about").
    /// </summary>
    static abstract string Route { get; }
}
