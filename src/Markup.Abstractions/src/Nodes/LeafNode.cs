using Femur.Parsing.Nodes;

namespace Femur.Markup.Abstractions.Nodes;

/// <summary>
/// Base class for nodes that cannot contain child nodes (leaf nodes).
/// Used by HTML, XML, and CHTML parsers.
/// </summary>
public abstract class LeafNode : Node
{
    // Leaf nodes cannot have children - this is enforced by the type system
}