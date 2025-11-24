using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// List item node.
/// Can contain block-level nodes (paragraphs, lists, etc.).
/// </summary>
public class ListItemNode : MarkdownContainerNode
{
    public override NodeType NodeType => MarkdownNodeType.ListItem;
}