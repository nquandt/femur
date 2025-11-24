using Femur.Parsing.Nodes;

namespace Femur.Markup.Abstractions.Nodes;

/// <summary>
/// Text content node - cannot have children.
/// Used by HTML, XML, and CHTML parsers.
/// </summary>
public class TextNode : LeafNode
{
    public override NodeType NodeType => Femur.Parsing.Nodes.NodeType.Text;

    /// <summary>
    /// The text content
    /// </summary>
    public string Content { get; set; } = string.Empty;
}