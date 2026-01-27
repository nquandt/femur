

namespace Femur.Chtml.Runtime;

/// <summary>
/// Empty props type for components that don't require any properties.
/// Use this as the type parameter for IRenderable when a component has no props.
/// </summary>
public static class EmptyProps
{
    /// <summary>
    /// Singleton instance of empty props. Components without props can use this.
    /// </summary>
    public static readonly EmptyPropsInstance Instance = new();
}

