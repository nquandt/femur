using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Thematic break node (horizontal rule).
/// Leaf node with no content.
/// </summary>
public class ThematicBreakNode : MarkdownLeafNode
{
    public override NodeType NodeType => MarkdownNodeType.ThematicBreak;
}