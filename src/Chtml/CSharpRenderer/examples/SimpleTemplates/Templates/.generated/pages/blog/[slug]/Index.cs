using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Pages.Blog.Slug;

public class IndexProps
{
    public required System.String Slug { get; set; }
}

public partial class Index : IRenderablePage<IndexProps, Templates.Generated.GlobalProps>
{
    public static string Route => "/blog/{slug}";

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
        await writer.WriteAsync(props.Slug?.ToString() ?? string.Empty);
        await writer.WriteAsync(" - Blog - ");
        await writer.WriteAsync(globalProps.SiteName ?? "My Site"?.ToString() ?? string.Empty);
        await writer.WriteAsync("</title>");
        await writer.WriteAsync("<style>");
        await writer.WriteAsync("body { font-family: system-ui, sans-serif; margin: 0; padding: 0; }\n        .container { max-width: 1200px; margin: 0 auto; padding: 2rem; }");
        await writer.WriteAsync("</style>");
        await writer.WriteAsync("</head>");
        await writer.WriteAsync("<body");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<Templates.Generated.Components.Header");
        await writer.WriteAsync(" Title=\"Blog Post\"");
        await writer.WriteAsync(" ShowNavigation=\"true\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("<main");
        await writer.WriteAsync(" class=\"container\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<Templates.Generated.Components.BlogPost");
        await writer.WriteAsync($" Title={props.Slug}");
        await writer.WriteAsync(" Author=\"John Doe\"");
        await writer.WriteAsync($" PublishedDate={System.DateTime.Now}");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("This is the content for the blog post: ");
        await writer.WriteAsync(props.Slug?.ToString() ?? string.Empty);
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("This demonstrates dynamic routing with bracket notation [slug].");
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("</Templates.Generated.Components.BlogPost>");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"mt-8\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<Templates.Generated.Components.Button");
        await writer.WriteAsync(" Text=\"Back to Blog\"");
        await writer.WriteAsync(" Href=\"/blog\"");
        await writer.WriteAsync(" Variant=\"secondary\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("</main>");
        await writer.WriteAsync("</body>");
        await writer.WriteAsync("</html>");
    }
}
