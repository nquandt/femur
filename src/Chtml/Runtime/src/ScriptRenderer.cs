

using System.Collections.Generic;

namespace Femur.Chtml.Runtime;

/// <summary>
/// Static methods for rendering script tags.
/// Used by generated code to render hoisted scripts.
/// </summary>
public static class ScriptRenderer
{
    /// <summary>
    /// Renders script tags for the given script IDs.
    /// </summary>
    public static void RenderScripts(MetaWriter writer, IEnumerable<string> scriptIds)
    {
        foreach (var scriptId in scriptIds)
        {
            writer.WriteNewLine();
            writer.Write($"<script src=\"/script/{scriptId}\" defer></script>");
            writer.WriteNewLine();
        }
    }
}
