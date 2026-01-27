using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Pages.Contact;

public class IndexProps
{
    public required System.String ContactEmail { get; set; }
}

public partial class Index : IRenderablePage<IndexProps, Templates.Generated.GlobalProps>
{
    public static string Route => "/contact";

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
        await writer.WriteAsync("Contact - ");
        await writer.WriteAsync(globalProps.SiteName ?? "My Site"?.ToString() ?? string.Empty);
        await writer.WriteAsync("</title>");
        await writer.WriteAsync("<style>");
        await writer.WriteAsync("body { font-family: system-ui, sans-serif; margin: 0; padding: 0; }\n        .container { max-width: 800px; margin: 0 auto; padding: 2rem; }\n        form { display: flex; flex-direction: column; gap: 1rem; }\n        input, textarea { padding: 0.5rem; border: 1px solid #ccc; border-radius: 4px; }\n        button { padding: 0.75rem; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; }");
        await writer.WriteAsync("</style>");
        await writer.WriteAsync("</head>");
        await writer.WriteAsync("<body");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<Templates.Generated.Components.Header");
        await writer.WriteAsync(" Title=\"Contact Us\"");
        await writer.WriteAsync(" ShowNavigation=\"true\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("<main");
        await writer.WriteAsync(" class=\"container\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<h1");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Get in Touch");
        await writer.WriteAsync("</h1>");
        await writer.WriteAsync("<p");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Email us at: ");
        await writer.WriteAsync("<a");
        await writer.WriteAsync(" href=\"mailto:{props.ContactEmail}\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync(props.ContactEmail?.ToString() ?? string.Empty);
        await writer.WriteAsync("</a>");
        await writer.WriteAsync("</p>");
        await writer.WriteAsync("<form");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<label");
        await writer.WriteAsync(" for=\"name\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Name:");
        await writer.WriteAsync("</label>");
        await writer.WriteAsync("<input");
        await writer.WriteAsync(" type=\"text\"");
        await writer.WriteAsync(" id=\"name\"");
        await writer.WriteAsync(" name=\"name\"");
        await writer.WriteAsync(" required=\"\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<label");
        await writer.WriteAsync(" for=\"email\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Email:");
        await writer.WriteAsync("</label>");
        await writer.WriteAsync("<input");
        await writer.WriteAsync(" type=\"email\"");
        await writer.WriteAsync(" id=\"email\"");
        await writer.WriteAsync(" name=\"email\"");
        await writer.WriteAsync(" required=\"\"");
        await writer.WriteAsync(" />");
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("<div");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<label");
        await writer.WriteAsync(" for=\"message\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Message:");
        await writer.WriteAsync("</label>");
        await writer.WriteAsync("<textarea");
        await writer.WriteAsync(" id=\"message\"");
        await writer.WriteAsync(" name=\"message\"");
        await writer.WriteAsync(" rows=\"5\"");
        await writer.WriteAsync(" required=\"\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("</textarea>");
        await writer.WriteAsync("</div>");
        await writer.WriteAsync("<button");
        await writer.WriteAsync(" type=\"submit\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("Send Message");
        await writer.WriteAsync("</button>");
        await writer.WriteAsync("</form>");
        await writer.WriteAsync("</main>");
        await writer.WriteAsync("</body>");
        await writer.WriteAsync("</html>");
    }
}
