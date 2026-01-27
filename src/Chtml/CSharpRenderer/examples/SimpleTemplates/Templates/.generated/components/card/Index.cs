using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Components.Card;

public class IndexProps
{
    public required System.String Title { get; set; }
    public required RenderPipe<Templates.Generated.GlobalProps> Content { get; set; }
}

public partial class Index : IRenderable<IndexProps, Templates.Generated.GlobalProps>
{
    public static Type[] DependsOn() => Array.Empty<Type>();

    public static async ValueTask RenderAsync(RenderContext<Templates.Generated.GlobalProps> renderContext, IndexProps inputProps, params RenderPipe<Templates.Generated.GlobalProps>[] children)
    {
        var (writer, globalProps) = renderContext;

        var props = inputProps;

        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"card bg-white rounded-lg shadow-md p-6\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<h2");
        await writer.WriteAsync(" class=\"text-xl font-semibold mb-4\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync(props.Title?.ToString() ?? string.Empty);
        await writer.WriteAsync("</h2>");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"card-content\"");
        await writer.WriteAsync(">");
        await renderContext.RenderAsync(children);
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("</div>");
    }
}
