using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Code span node (`code`).
/// Contains literal text content.
/// </summary>
public class CodeSpanNode : MarkdownLeafNode
{
    public override NodeType NodeType => MarkdownNodeType.CodeSpan;

    /// <summary>
    /// The code content
    /// </summary>
    public string Content { get; set; } = string.Empty;
}