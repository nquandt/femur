namespace Femur.Parsing.Nodes;

/// <summary>
/// Base class for all AST nodes
/// </summary>
public abstract class Node
{
    /// <summary>
    /// The parent node of this node, or null if root
    /// </summary>
    public Node? Parent { get; set; }

    /// <summary>
    /// The node type
    /// </summary>
    public abstract NodeType NodeType { get; }

    /// <summary>
    /// The location of this node in the source stream
    /// </summary>
    public SourceLocation Location { get; set; }
}