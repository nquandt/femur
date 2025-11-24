using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Markdown text content node - cannot have children.
/// Markdown-specific text node type.
/// </summary>
public class MarkdownTextNode : MarkdownLeafNode
{
    public override NodeType NodeType => NodeType.Text;

    /// <summary>
    /// The text content
    /// </summary>
    public string Content { get; set; } = string.Empty;
}