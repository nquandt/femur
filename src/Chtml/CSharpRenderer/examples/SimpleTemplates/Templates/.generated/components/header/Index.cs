using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Components.Header;

public class IndexProps
{
    public required System.String Title { get; set; }
    public required System.Boolean ShowNavigation { get; set; }
}

public partial class Index : IRenderable<IndexProps, Templates.Generated.GlobalProps>
{
    public static Type[] DependsOn() => Array.Empty<Type>();

    public static async ValueTask RenderAsync(RenderContext<Templates.Generated.GlobalProps> renderContext, IndexProps inputProps, params RenderPipe<Templates.Generated.GlobalProps>[] children)
    {
        var (writer, globalProps) = renderContext;

        var props = inputProps;

        await writer.WriteAsync("<header");
        await writer.WriteAsync(" class=\"bg-white shadow-sm\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"container mx-auto px-4 py-4\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<h1");
        await writer.WriteAsync(" class=\"text-2xl font-bold\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync(props.Title?.ToString() ?? string.Empty);
        await writer.WriteAsync("</h1>");
        await writer.WriteAsync("\n        @if (props.ShowNavigation)\n        ");
        await writer.WriteAsync(<nav class="mt-4">
                <a href="/" class="mr-4">Home</a>
                <a href="/about" class="mr-4">About</a>
                <a href="/blog" class="mr-4">Blog</a>
            </nav>?.ToString() ?? string.Empty);
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("</header>");
    }
}
