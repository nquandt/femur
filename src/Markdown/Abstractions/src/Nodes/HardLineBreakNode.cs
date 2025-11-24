using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Hard line break node (two spaces + newline or backslash + newline).
/// Leaf node with no content.
/// </summary>
public class HardLineBreakNode : MarkdownLeafNode
{
    public override NodeType NodeType => MarkdownNodeType.HardLineBreak;
}