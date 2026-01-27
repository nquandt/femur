using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Components.Layout;

public class IndexProps
{
    public required System.String PageTitle { get; set; }
}

public partial class Index : IRenderable<IndexProps, Templates.Generated.GlobalProps>
{
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
        await writer.WriteAsync(props.PageTitle?.ToString() ?? string.Empty);
        await writer.WriteAsync(" - ");
        await writer.WriteAsync(globalProps.SiteName ?? "My Site"?.ToString() ?? string.Empty);
        await writer.WriteAsync("</title>");
        await writer.WriteAsync("<style>");
        await writer.WriteAsync("body { \n            font-family: system-ui, -apple-system, sans-serif; \n            margin: 0; \n            padding: 0; \n            background-color: #f5f5f5;\n        }\n        .main-content {\n            min-height: calc(100vh - 120px);\n            padding: 2rem 0;\n        }");
        await writer.WriteAsync("</style>");
        await writer.WriteAsync("</head>");
        await writer.WriteAsync("<body");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<Templates.Generated.Components.Header");
        await writer.WriteAsync(" Title=\"{globalProps.SiteName ?? \"");
        await writer.WriteAsync(" My=\"\"");
        await writer.WriteAsync(" Site"}"=\"\"");
        await writer.WriteAsync(" ShowNavigation=\"true\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("<main");
        await writer.WriteAsync(" class=\"main-content\"");
        await writer.WriteAsync(">");
        await renderContext.RenderAsync(children);
        await writer.WriteAsync("</main>");
        await writer.WriteAsync("<footer");
        await writer.WriteAsync(" class=\"bg-gray-800 text-white p-4 text-center\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("&copy; 2024 ");
        await writer.WriteAsync(globalProps.SiteName ?? "My Site"?.ToString() ?? string.Empty);
        await writer.WriteAsync(". All rights reserved.");
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Theme: ");
        await writer.WriteAsync(globalProps.Theme ?? "default"?.ToString() ?? string.Empty);
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("</footer>");
        await writer.WriteAsync("</body>");
        await writer.WriteAsync("</html>");
    }
}
