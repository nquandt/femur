using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// HTML block node (raw HTML in Markdown).
/// Contains literal HTML content.
/// </summary>
public class HtmlBlockNode : MarkdownLeafNode
{
    public override NodeType NodeType => MarkdownNodeType.HtmlBlock;

    /// <summary>
    /// The raw HTML content
    /// </summary>
    public string Content { get; set; } = string.Empty;
}