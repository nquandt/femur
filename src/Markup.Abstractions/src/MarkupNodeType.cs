using Femur.Parsing.Nodes;

namespace Femur.Markup.Abstractions;

/// <summary>
/// Markup-specific node types (HTML, XML, CHTML).
/// </summary>
public static class MarkupNodeType
{
    /// <summary>
    /// Document node type
    /// </summary>
    public static NodeType Document { get; } = NodeType.Custom("Document");

    /// <summary>
    /// Element node type
    /// </summary>
    public static NodeType Element { get; } = NodeType.Custom("Element");

    /// <summary>
    /// Comment node type
    /// </summary>
    public static NodeType Comment { get; } = NodeType.Custom("Comment");

    /// <summary>
    /// Document type declaration node type
    /// </summary>
    public static NodeType DocumentType { get; } = NodeType.Custom("DocumentType");

    /// <summary>
    /// CDATA section node type
    /// </summary>
    public static NodeType CData { get; } = NodeType.Custom("CData");
}