

using System;
using System.Threading.Tasks;

namespace Femur.Chtml.Runtime;


/// <summary>
/// Interface for renderable components with static abstract methods.
/// Components implement this interface to provide rendering functionality.
/// </summary>
/// <typeparam name="TInputProps">The props type that callers pass (public API).</typeparam>
/// <typeparam name="TGlobalProps">The global props type.</typeparam>
public interface IRenderable<TInputProps, TGlobalProps>
    where TInputProps : class
    where TGlobalProps : class
{
    /// <summary>
    /// Renders the component asynchronously.
    /// </summary>
    /// <param name="renderContext">The RenderContext containing the writer and global props</param>
    /// <param name="inputProps">The input props passed by the caller</param>
    /// <param name="children">The child components to render</param>
    /// <returns>A ValueTask representing the async rendering operation</returns>
    static abstract ValueTask RenderAsync(RenderContext<TGlobalProps> renderContext, TInputProps inputProps, params RenderPipe<TGlobalProps>[] children);

    /// <summary>
    /// Returns the direct component dependencies used by this component.
    /// Used for script hoisting and dependency analysis.
    /// Returns only direct dependencies (not transitive).
    /// </summary>
    /// <returns>Array of component types that this component directly depends on</returns>
    static abstract Type[] DependsOn();
}

