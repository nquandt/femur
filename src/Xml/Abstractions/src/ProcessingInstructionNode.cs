using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;

namespace Femur.Xml.Abstractions;

/// <summary>
/// Processing instruction node (&lt;?target data?&gt;)
/// XML-specific feature.
/// </summary>
public class ProcessingInstructionNode : LeafNode
{
    public override NodeType NodeType => XmlNodeType.ProcessingInstruction;

    /// <summary>
    /// The target name (e.g., "xml" in &lt;?xml version="1.0"?&gt;)
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// The processing instruction data/content
    /// </summary>
    public string Content { get; set; } = string.Empty;
}