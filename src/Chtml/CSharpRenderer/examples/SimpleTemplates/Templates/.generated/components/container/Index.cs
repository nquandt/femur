using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Components.Container;

public partial class Index : IRenderable<EmptyPropsInstance, Templates.Generated.GlobalProps>
{
    public static Type[] DependsOn() => Array.Empty<Type>();

    public static async ValueTask RenderAsync(RenderContext<Templates.Generated.GlobalProps> renderContext, EmptyPropsInstance inputProps, params RenderPipe<Templates.Generated.GlobalProps>[] children)
    {
        var (writer, globalProps) = renderContext;

        var props = inputProps;

        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"not-print:relative w-full flex justify-center\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"w-full max-w-screen-lg flex flex-row px-6 sm:px-12 items-center\"");
        await writer.WriteAsync(">");
        await renderContext.RenderAsync(children);
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("</div>");
    }
}
