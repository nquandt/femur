using Femur.Parsing.Nodes;
using static Femur.Markup.Abstractions.MarkupNodeType;

namespace Femur.Markup.Abstractions.Nodes;

/// <summary>
/// Root document node containing all top-level nodes.
/// Used by HTML, XML, and CHTML parsers.
/// </summary>
public class DocumentNode : ContainerNode
{
    public override NodeType NodeType => Document;
}