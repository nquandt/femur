using Femur.Markup.Abstractions.Nodes;

namespace Femur.Xml.Abstractions;

/// <summary>
/// XML document node.
/// Extends DocumentNode for XML-specific features.
/// </summary>
public class XmlDocumentNode : DocumentNode
{
    /// <summary>
    /// XML declaration (&lt;?xml version="1.0"?&gt;)
    /// </summary>
    public ProcessingInstructionNode? XmlDeclaration { get; set; }
}