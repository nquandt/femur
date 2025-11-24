using Femur.Parsing.Nodes;

namespace Femur.Markup.Abstractions.Nodes;

/// <summary>
/// Document type declaration (&lt;!DOCTYPE ...&gt;) - cannot have children
/// </summary>
public class DocumentTypeNode : LeafNode
{
    public override NodeType NodeType => MarkupNodeType.DocumentType;

    /// <summary>
    /// The doctype content
    /// </summary>
    public string Content { get; set; } = string.Empty;
}