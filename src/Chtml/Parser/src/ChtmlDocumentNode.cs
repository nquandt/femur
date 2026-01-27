using Femur.Markup.Abstractions.Nodes;

namespace Femur.Chtml.Parser;

/// <summary>
/// CHTML-specific document node that extends DocumentNode with front matter support.
/// </summary>
public class ChtmlDocumentNode : DocumentNode
{
    /// <summary>
    /// Optional front matter YAML content (CHTML-specific)
    /// </summary>
    public Dictionary<string, object>? FrontMatter { get; set; }

    /// <summary>
    /// Raw front matter text (CHTML-specific)
    /// </summary>
    public string? FrontMatterRaw { get; set; }
}