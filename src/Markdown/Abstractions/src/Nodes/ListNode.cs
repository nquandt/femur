using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// List node (ordered or unordered).
/// Contains ListItemNode children.
/// </summary>
public class ListNode : MarkdownContainerNode
{
    public override NodeType NodeType => MarkdownNodeType.List;

    /// <summary>
    /// Whether this is an ordered list (true) or unordered list (false)
    /// </summary>
    public bool IsOrdered { get; set; }

    /// <summary>
    /// For ordered lists, the starting number (default 1)
    /// </summary>
    public int StartNumber { get; set; } = 1;

    /// <summary>
    /// The list marker character for unordered lists ('-', '*', or '+')
    /// </summary>
    public char BulletChar { get; set; } = '-';

    /// <summary>
    /// Whether the list is tight (false) or loose (true)
    /// </summary>
    public bool IsLoose { get; set; }
}