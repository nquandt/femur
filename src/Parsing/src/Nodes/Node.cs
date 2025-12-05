namespace Femur.Parsing.Nodes;

/// <summary>
/// Base class for all AST nodes
/// </summary>
public abstract class Node
{
    private ParentNode? _parent;
    private Node? _previousSibling;
    private Node? _nextSibling;
    private int _siblingIndex = -1;

    /// <summary>
    /// Gets the parent node of this node, or null if this is a root node.
    /// </summary>
    /// <returns>The parent node, or null if this node is a root</returns>
    public ParentNode? GetParent() => this._parent;

    /// <summary>
    /// Sets the parent node of this node.
    /// </summary>
    /// <param name="parent">The parent node, or null if this is a root node</param>
    public void SetParent(ParentNode? parent) => this._parent = parent;

    /// <summary>
    /// Gets the previous sibling node in the parent's children list, or null if this is the first child.
    /// </summary>
    /// <returns>The previous sibling node, or null if this is the first child or has no parent</returns>
    public Node? GetPreviousSibling() => this._previousSibling;

    /// <summary>
    /// Gets the next sibling node in the parent's children list, or null if this is the last child.
    /// </summary>
    /// <returns>The next sibling node, or null if this is the last child or has no parent</returns>
    public Node? GetNextSibling() => this._nextSibling;

    /// <summary>
    /// Gets the index of this node in its parent's children list.
    /// Returns -1 if this node has no parent.
    /// </summary>
    /// <returns>The zero-based index in the parent's children list, or -1 if no parent</returns>
    public int GetSiblingIndex() => this._siblingIndex;

    /// <summary>
    /// Gets all ancestor nodes from immediate parent to root.
    /// </summary>
    /// <returns>An enumerable of ancestor nodes, starting with the immediate parent</returns>
    public IEnumerable<Node> GetAncestors()
    {
        var current = this._parent;
        while (current != null)
        {
            yield return current;
            current = current._parent;
        }
    }

    /// <summary>
    /// Sets the sibling navigation references for this node.
    /// Called automatically by parsers when adding nodes to a parent.
    /// </summary>
    /// <param name="siblingIndex">The index of this node in the parent's children list</param>
    /// <param name="previousSibling">The previous sibling, or null if this is the first child</param>
    /// <param name="nextSibling">The next sibling, or null if this is the last child</param>
    internal void SetSiblingReferences(int siblingIndex, Node? previousSibling, Node? nextSibling)
    {
        this._siblingIndex = siblingIndex;
        this._previousSibling = previousSibling;
        this._nextSibling = nextSibling;
    }

    /// <summary>
    /// The node type
    /// </summary>
    public abstract NodeType NodeType { get; }

    /// <summary>
    /// The location of this node in the source stream
    /// </summary>
    public SourceLocation Location { get; set; }
}