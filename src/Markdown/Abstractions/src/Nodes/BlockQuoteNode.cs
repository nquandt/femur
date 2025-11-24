using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Block quote node.
/// Can contain other block-level nodes.
/// </summary>
public class BlockQuoteNode : MarkdownContainerNode
{
    public override NodeType NodeType => MarkdownNodeType.BlockQuote;
}