using Femur.Parsing.Nodes;

namespace Femur.Markup.Abstractions.Nodes;

/// <summary>
/// Base class for nodes that can contain child nodes.
/// Used by HTML, XML, and CHTML parsers.
/// </summary>
public abstract class ContainerNode : Node
{
    private List<Node>? _children;

    /// <summary>
    /// Child nodes of this node.
    /// List is lazily initialized to reduce allocations for nodes without children.
    /// </summary>
    public List<Node> Children
    {
        get
        {
            this._children ??= new List<Node>();
            return this._children;
        }
    }

    /// <summary>
    /// Returns true if this node has any children
    /// </summary>
    public bool HasChildren => this._children != null && this._children.Count > 0;
}