using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Soft line break node (single newline within a paragraph).
/// Leaf node with no content.
/// </summary>
public class SoftLineBreakNode : MarkdownLeafNode
{
    public override NodeType NodeType => MarkdownNodeType.SoftLineBreak;
}