
namespace Femur.Parsing.Nodes;

public abstract class ParentNode : Node
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

    /// <summary>
    /// Updates sibling references for all children in this container.
    /// Called automatically by parsers after adding a child node.
    /// Maintains bidirectional sibling links for efficient tree navigation.
    /// </summary>
    internal void UpdateSiblingReferences()
    {
        if (this._children == null || this._children.Count == 0)
        {
            return;
        }

        for (var i = 0; i < this._children.Count; i++)
        {
            var child = this._children[i];
            var previousSibling = i > 0 ? this._children[i - 1] : null;
            var nextSibling = i < this._children.Count - 1 ? this._children[i + 1] : null;

            child.SetSiblingReferences(i, previousSibling, nextSibling);
        }
    }
}