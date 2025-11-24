using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Link node [text](url) or [text][ref].
/// Can contain inline content for the link text.
/// </summary>
public class LinkNode : MarkdownContainerNode
{
    public override NodeType NodeType => MarkdownNodeType.Link;

    /// <summary>
    /// The link URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The link title (optional)
    /// </summary>
    public string? Title { get; set; }
}