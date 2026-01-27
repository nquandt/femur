using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Pages.Blog;

public class IndexProps
{
    public required System.Collections.Generic.List<System.String> Posts { get; set; }
}

public partial class Index : IRenderablePage<IndexProps, Templates.Generated.GlobalProps>
{
    public static string Route => "/blog";

    public static Type[] DependsOn() => Array.Empty<Type>();

    public static async ValueTask RenderAsync(RenderContext<Templates.Generated.GlobalProps> renderContext, IndexProps inputProps, params RenderPipe<Templates.Generated.GlobalProps>[] children)
    {
        var (writer, globalProps) = renderContext;

        var props = inputProps;

        await writer.WriteAsync("<html");
        await writer.WriteAsync(" lang=\"{globalProps.Language ?? \"");
        await writer.WriteAsync(" en"}"=\"\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<head");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<meta");
        await writer.WriteAsync(" charset=\"UTF-8\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("<meta");
        await writer.WriteAsync(" name=\"viewport\"");
        await writer.WriteAsync(" content=\"width=device-width, initial-scale=1.0\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("<title");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Blog - ");
        await writer.WriteAsync(globalProps.SiteName ?? "My Site"?.ToString() ?? string.Empty);
        await writer.WriteAsync("</title>");
        await writer.WriteAsync("<style>");
        await writer.WriteAsync("body { font-family: system-ui, sans-serif; margin: 0; padding: 0; }\n        .container { max-width: 1200px; margin: 0 auto; padding: 2rem; }\n        .post-list { display: grid; gap: 2rem; }");
        await writer.WriteAsync("</style>");
        await writer.WriteAsync("</head>");
        await writer.WriteAsync("<body");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<Templates.Generated.Components.Header");
        await writer.WriteAsync(" Title=\"Blog\"");
        await writer.WriteAsync(" ShowNavigation=\"true\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("<main");
        await writer.WriteAsync(" class=\"container\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<h1");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Blog Posts");
        await writer.WriteAsync("</h1>");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"post-list\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("\n            @foreach (var postTitle in props.Posts)\n            ");
        await writer.WriteAsync(<Templates.Generated.Components.Card Title="{postTitle?.ToString() ?? string.Empty);
        await writer.WriteAsync("\">\n                    ");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Read more about ");
        await writer.WriteAsync(postTitle?.ToString() ?? string.Empty);
        await writer.WriteAsync("...");
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("<Templates.Generated.Components.Button");
        await writer.WriteAsync(" Text=\"Read More\"");
        await writer.WriteAsync(" Href=\"/blog/{postTitle}\"");
        await writer.WriteAsync(" Variant=\"primary\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("</main>");
        await writer.WriteAsync("</body>");
        await writer.WriteAsync("</html>");
        await writer.WriteAsync("\n            }\n        ");
    }
}
