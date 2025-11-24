using Femur.Parsing.Nodes;

namespace Femur.Markup.Abstractions.Nodes;

/// <summary>
/// Comment node (&lt;!-- ... --&gt;) - cannot have children
/// </summary>
public class CommentNode : LeafNode
{
    public override NodeType NodeType => MarkupNodeType.Comment;

    /// <summary>
    /// The comment content
    /// </summary>
    public string Content { get; set; } = string.Empty;
}