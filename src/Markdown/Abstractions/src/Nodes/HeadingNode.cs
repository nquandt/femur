using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Heading node (ATX or Setext style).
/// Can contain inline content.
/// </summary>
public class HeadingNode : MarkdownContainerNode
{
    public override NodeType NodeType => MarkdownNodeType.Heading;

    /// <summary>
    /// Heading level (1-6)
    /// </summary>
    public int Level { get; set; }
}