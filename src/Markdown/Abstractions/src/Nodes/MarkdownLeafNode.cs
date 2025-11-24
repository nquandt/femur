using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Base class for Markdown nodes that cannot contain child nodes (leaf nodes).
/// Markdown-specific leaf node type.
/// </summary>
public abstract class MarkdownLeafNode : Node
{
    // Leaf nodes cannot have children - this is enforced by the type system
}