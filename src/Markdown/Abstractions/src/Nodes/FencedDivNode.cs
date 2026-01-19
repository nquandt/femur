using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Represents a fenced div (generic container block) in a Markdown document.
/// A fenced div is a container block delimited by lines of three or more colons (:::)
/// and can contain any block-level content including nested divs.
/// This implements the Pandoc fenced_divs extension.
/// </summary>
public class FencedDivNode : MarkdownContainerNode
{
    /// <summary>
    /// The tag/identifier of the fenced div (e.g., "Codeblock" in :::Codeblock {lang="csharp"}).
    /// This is the identifier that appears after the colons and before the attributes.
    /// When rendering, this determines the HTML tag type (e.g., "a" → &lt;a&gt;, "warning" → &lt;div class="warning"&gt;).
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// The attributes of the fenced div, specified in the opening fence.
    /// Attributes are in Pandoc format: {#id .class1 .class2 key=value}
    /// </summary>
    public string Attributes { get; set; } = string.Empty;

    /// <summary>
    /// Parsed attributes from the attributes string.
    /// Contains id, classes, and key-value pairs.
    /// </summary>
    public FencedDivAttributes ParsedAttributes { get; set; } = new();

    /// <summary>
    /// The number of colons used in the opening fence (for visual clarity in nested divs).
    /// </summary>
    public int OpeningFenceLength { get; set; } = 3;

    /// <summary>
    /// The raw text content of the fenced div (without the opening and closing fences).
    /// This contains the unparsed markdown content inside the div, similar to how CodeBlockNode.Content works.
    /// </summary>
    public string RawContent { get; set; } = string.Empty;

    public override NodeType NodeType => MarkdownNodeType.FencedDiv;
}

/// <summary>
/// Represents parsed attributes from a fenced div opening fence.
/// Attributes follow the Pandoc format: {#id .class1 .class2 key=value}
/// </summary>
public class FencedDivAttributes
{
    /// <summary>
    /// The ID attribute (if specified with #id syntax).
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// CSS classes (if specified with .class syntax).
    /// </summary>
    public List<string> Classes { get; set; } = new();

    /// <summary>
    /// Custom key-value attributes (key=value or key="value").
    /// </summary>
    public Dictionary<string, string> KeyValueAttributes { get; set; } = new();
}
