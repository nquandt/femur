using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Code block node (indented or fenced).
/// Contains literal text content.
/// </summary>
public class CodeBlockNode : MarkdownLeafNode
{
    public override NodeType NodeType => MarkdownNodeType.CodeBlock;

    /// <summary>
    /// The code content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Info string for fenced code blocks (language identifier)
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// Whether this is a fenced code block (true) or indented code block (false)
    /// </summary>
    public bool IsFenced { get; set; }
}