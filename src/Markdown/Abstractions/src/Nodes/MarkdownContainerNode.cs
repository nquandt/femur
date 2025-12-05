using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Base class for Markdown nodes that can contain child nodes.
/// Markdown-specific container node type.
/// </summary>
public abstract class MarkdownContainerNode : ParentNode
{
}