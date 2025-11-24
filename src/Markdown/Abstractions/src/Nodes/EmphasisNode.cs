using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Emphasis node (*text* or _text_).
/// Can contain inline content.
/// </summary>
public class EmphasisNode : MarkdownContainerNode
{
    public override NodeType NodeType => MarkdownNodeType.Emphasis;
}