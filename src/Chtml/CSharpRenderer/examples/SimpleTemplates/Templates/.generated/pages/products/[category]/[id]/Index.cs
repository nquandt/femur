using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Pages.Products.Category.Id;

public class IndexProps
{
    public required System.String Category { get; set; }
    public required System.String Id { get; set; }
}

public partial class Index : IRenderablePage<IndexProps, Templates.Generated.GlobalProps>
{
    public static string Route => "/products/{category}/{id}";

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
        await writer.WriteAsync("Product ");
        await writer.WriteAsync(props.Id?.ToString() ?? string.Empty);
        await writer.WriteAsync(" in ");
        await writer.WriteAsync(props.Category?.ToString() ?? string.Empty);
        await writer.WriteAsync(" - ");
        await writer.WriteAsync(globalProps.SiteName ?? "My Site"?.ToString() ?? string.Empty);
        await writer.WriteAsync("</title>");
        await writer.WriteAsync("<style>");
        await writer.WriteAsync("body { font-family: system-ui, sans-serif; margin: 0; padding: 0; }\n        .container { max-width: 1200px; margin: 0 auto; padding: 2rem; }");
        await writer.WriteAsync("</style>");
        await writer.WriteAsync("</head>");
        await writer.WriteAsync("<body");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<Templates.Generated.Components.Header");
        await writer.WriteAsync(" Title=\"Product Details\"");
        await writer.WriteAsync(" ShowNavigation=\"true\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("<main");
        await writer.WriteAsync(" class=\"container\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<h1");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Product Details");
        await writer.WriteAsync("</h1>");
        await writer.WriteAsync("<Templates.Generated.Components.Card");
        await writer.WriteAsync(" Title=\"Product Information\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<strong");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Category:");
        await writer.WriteAsync("</strong>");
        await writer.WriteAsync(props.Category?.ToString() ?? string.Empty);
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<strong");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Product ID:");
        await writer.WriteAsync("</strong>");
        await writer.WriteAsync(props.Id?.ToString() ?? string.Empty);
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("This demonstrates nested dynamic routes with multiple parameters.");
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("</Templates.Generated.Components.Card>");
        await writer.WriteAsync("</main>");
        await writer.WriteAsync("</body>");
        await writer.WriteAsync("</html>");
    }
}
