using Femur.Parsing.Nodes;

namespace Femur.Markup.Abstractions.Nodes;

/// <summary>
/// CDATA section (&lt;![CDATA[...]]&gt;) - cannot have children
/// </summary>
public class CDataNode : LeafNode
{
    public override NodeType NodeType => MarkupNodeType.CData;

    /// <summary>
    /// The CDATA content
    /// </summary>
    public string Content { get; set; } = string.Empty;
}