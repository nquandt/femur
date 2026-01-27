using System;
using System.Threading.Tasks;
using Femur.Chtml.Runtime;
using System.IO.Pipelines;
using Templates.Generated;

namespace Templates.Generated.Components.Alert;

public class IndexInputProps
{
    public required System.String Message { get; set; }
    public required System.String Type { get; set; }
}

public class IndexProps : IndexInputProps
{
    public required System.String AlertClass { get; set; }
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

        await writer.WriteAsync("<div");
        await writer.WriteAsync(" class=\"alert {props.AlertClass}\"");
        await writer.WriteAsync(">");
        await writer.WriteAsync("<strong");
        await writer.WriteAsync(">");
        await writer.WriteAsync(props.Type?.ToString() ?? string.Empty);
        await writer.WriteAsync(":");
        await writer.WriteAsync("</strong>");
        await writer.WriteAsync(props.Message?.ToString() ?? string.Empty);
        await writer.WriteAsync("</div>");
    }
}
