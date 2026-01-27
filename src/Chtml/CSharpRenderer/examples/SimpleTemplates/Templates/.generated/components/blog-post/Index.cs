using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Components.BlogPost;

public class IndexProps
{
    public required System.String Title { get; set; }
    public required System.String Author { get; set; }
    public required System.DateTime PublishedDate { get; set; }
    public required RenderPipe<Templates.Generated.GlobalProps> Content { get; set; }
}

public partial class Index : IRenderable<IndexProps, Templates.Generated.GlobalProps>
{
    public static Type[] DependsOn() => Array.Empty<Type>();

    public static async ValueTask RenderAsync(RenderContext<Templates.Generated.GlobalProps> renderContext, IndexProps inputProps, params RenderPipe<Templates.Generated.GlobalProps>[] children)
    {
        var (writer, globalProps) = renderContext;

        var props = inputProps;

        await writer.WriteAsync("<article");
        await writer.WriteAsync(" class=\"blog-post\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<header");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<h1");
        await writer.WriteAsync(" class=\"text-3xl font-bold\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync(props.Title?.ToString() ?? string.Empty);
        await writer.WriteAsync("</h1>");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"meta text-gray-600\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<span");
        await writer.WriteAsync(">");
        await writer.WriteAsync("By ");
        await writer.WriteAsync(props.Author?.ToString() ?? string.Empty);
        await writer.WriteAsync("</span>");
        await writer.WriteAsync("<span");
        await writer.WriteAsync(">");
        await writer.WriteAsync(" • ");
        await writer.WriteAsync("</span>");
        await writer.WriteAsync("<time");
        await writer.WriteAsync(">");
        await writer.WriteAsync(props.PublishedDate:yyyy-MM-dd?.ToString() ?? string.Empty);
        await writer.WriteAsync("</time>");
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("</header>");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"content mt-6\"");
        await writer.WriteAsync(">");
        await renderContext.RenderAsync(children);
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("</article>");
    }
}
