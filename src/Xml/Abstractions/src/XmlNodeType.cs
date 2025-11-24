using Femur.Parsing.Nodes;

namespace Femur.Xml.Abstractions;

/// <summary>
/// XML-specific node types.
/// XML extends HTML node types with XML-specific constructs.
/// </summary>
public static class XmlNodeType
{
    /// <summary>
    /// XML-specific node types
    /// </summary>
    public static NodeType ProcessingInstruction { get; } = NodeType.Custom("ProcessingInstruction");
}