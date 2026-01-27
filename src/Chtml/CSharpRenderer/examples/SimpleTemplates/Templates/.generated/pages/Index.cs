using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Pages;

public class IndexProps
{
    public required System.String WelcomeMessage { get; set; }
}

public partial class Index : IRenderablePage<IndexProps, Templates.Generated.GlobalProps>
{
    public static string Route => "/";

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
        await writer.WriteAsync(globalProps.SiteName ?? "My Site"?.ToString() ?? string.Empty);
        await writer.WriteAsync("</title>");
        await writer.WriteAsync("<style>");
        await writer.WriteAsync("body { font-family: system-ui, sans-serif; margin: 0; padding: 0; }\n        .container { max-width: 1200px; margin: 0 auto; padding: 2rem; }");
        await writer.WriteAsync("</style>");
        await writer.WriteAsync("</head>");
        await writer.WriteAsync("<body");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<Templates.Generated.Components.Header");
        await writer.WriteAsync(" Title=\"{globalProps.SiteName ?? \"");
        await writer.WriteAsync(" Home"}"=\"\"");
        await writer.WriteAsync(" ShowNavigation=\"true\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("<main");
        await writer.WriteAsync(" class=\"container\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<h1");
        await writer.WriteAsync(">");
        await writer.WriteAsync(props.WelcomeMessage?.ToString() ?? string.Empty);
        await writer.WriteAsync("</h1>");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Welcome to our website! This is a sample page demonstrating CHTML templates.");
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"mt-8\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<Templates.Generated.Components.Card");
        await writer.WriteAsync(" Title=\"Features\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("This template system supports:");
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("<ul");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<li");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Components with props");
        await writer.WriteAsync("</li>");
        await writer.WriteAsync("<li");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Pages with routes");
        await writer.WriteAsync("</li>");
        await writer.WriteAsync("<li");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Global properties");
        await writer.WriteAsync("</li>");
        await writer.WriteAsync("<li");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Computed properties");
        await writer.WriteAsync("</li>");
        await writer.WriteAsync("</ul>");
        await writer.WriteAsync("</Templates.Generated.Components.Card>");
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("</main>");
        await writer.WriteAsync("</body>");
        await writer.WriteAsync("</html>");
    }
}
