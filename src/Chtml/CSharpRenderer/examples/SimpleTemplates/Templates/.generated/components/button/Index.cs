using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Components.Button;

public class IndexInputProps
{
    public required System.String Text { get; set; }
    public System.String? Href { get; set; }
    public required System.String Variant { get; set; }
}

public class IndexProps : IndexInputProps
{
    public required System.String ButtonClass { get; set; }
}

public partial class Index : IRenderable<IndexInputProps, Templates.Generated.GlobalProps>
{
    public static Type[] DependsOn() => Array.Empty<Type>();

    // Partial method stub: MUST be implemented in code-beside file (e.g., Index.partial.cs)
    private static partial IndexProps TransformProps(IndexInputProps inputProps, Templates.Generated.GlobalProps globalProps);

    public static async ValueTask RenderAsync(RenderContext<Templates.Generated.GlobalProps> renderContext, IndexInputProps inputProps, params RenderPipe<Templates.Generated.GlobalProps>[] children)
    {
        var (writer, globalProps) = renderContext;

        var props = TransformProps(inputProps, globalProps);

        await writer.WriteAsync("\n@if (props.Href != null)\n");
        await writer.WriteAsync(<a href="{props.Href?.ToString() ?? string.Empty);
        await writer.WriteAsync("\" class=\"");
        await writer.WriteAsync(props.ButtonClass?.ToString() ?? string.Empty);
        await writer.WriteAsync("\">\n        ");
        await writer.WriteAsync(props.Text?.ToString() ?? string.Empty);
        await writer.WriteAsync("\n}\nelse\n");
        await writer.WriteAsync(<button class="{props.ButtonClass?.ToString() ?? string.Empty);
        await writer.WriteAsync("\">\n        ");
        await writer.WriteAsync(props.Text?.ToString() ?? string.Empty);
        await writer.WriteAsync("\n}\n\n\n\n");
    }
}
