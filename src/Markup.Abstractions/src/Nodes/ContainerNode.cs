using Femur.Parsing.Nodes;

namespace Femur.Markup.Abstractions.Nodes;


/// <summary>
/// Base class for nodes that can contain child nodes.
/// Used by HTML, XML, and CHTML parsers.
/// </summary>
public abstract class ContainerNode : ParentNode
{
}
