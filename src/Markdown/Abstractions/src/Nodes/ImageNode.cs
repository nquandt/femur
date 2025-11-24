using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Image node ![alt](url) or ![alt][ref].
/// Contains alt text as inline content.
/// </summary>
public class ImageNode : MarkdownContainerNode
{
    public override NodeType NodeType => MarkdownNodeType.Image;

    /// <summary>
    /// The image URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The image title (optional)
    /// </summary>
    public string? Title { get; set; }
}