using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Paragraph node.
/// Can contain inline content.
/// </summary>
public class ParagraphNode : MarkdownContainerNode
{
    public override NodeType NodeType => MarkdownNodeType.Paragraph;
}