using Femur.Parsing.Nodes;

namespace Femur.Markdown.Extended.Abstractions;

/// <summary>
/// Extended Markdown-specific node types for YAML frontmatter support.
/// </summary>
public static class ExtendedMarkdownNodeType
{
    /// <summary>
    /// Extended document node type with frontmatter support
    /// </summary>
    public static NodeType ExtendedDocument { get; } = NodeType.Custom("ExtendedDocument");

    /// <summary>
    /// YAML frontmatter block node type
    /// </summary>
    public static NodeType FrontMatterBlock { get; } = NodeType.Custom("FrontMatterBlock");
}
