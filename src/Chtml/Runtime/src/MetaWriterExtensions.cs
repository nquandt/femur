

using System.Threading.Tasks;

namespace Femur.Chtml.Runtime;


public static class MetaWriterExtensions
{
    /// <summary>
    /// Helper to render an array of RenderPipe children from a RenderContext instance.
    /// Generated components can call this instead of emitting the foreach loop.
    /// </summary>
    public static async ValueTask RenderAsync<TGlobalProps>(this RenderContext<TGlobalProps> renderContext, params RenderPipe<TGlobalProps>[] children)
        where TGlobalProps : class
    {
        if (children == null)
        {
            return;
        }

        foreach (var c in children)
        {
            await c(renderContext);
        }
    }
}
