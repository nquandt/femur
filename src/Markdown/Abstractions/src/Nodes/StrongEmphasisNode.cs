using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Strong emphasis node (**text** or __text__).
/// Can contain inline content.
/// </summary>
public class StrongEmphasisNode : MarkdownContainerNode
{
    public override NodeType NodeType => MarkdownNodeType.StrongEmphasis;
}