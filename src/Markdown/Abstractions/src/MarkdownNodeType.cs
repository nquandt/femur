using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions;

/// <summary>
/// Markdown-specific node types.
/// </summary>
public static class MarkdownNodeType
{
    /// <summary>
    /// Document node type
    /// </summary>
    public static NodeType Document { get; } = NodeType.Custom("Document");
    // Block-level node types
    public static NodeType Heading { get; } = NodeType.Custom("Heading");
    public static NodeType Paragraph { get; } = NodeType.Custom("Paragraph");
    public static NodeType BlockQuote { get; } = NodeType.Custom("BlockQuote");
    public static NodeType CodeBlock { get; } = NodeType.Custom("CodeBlock");
    public static NodeType List { get; } = NodeType.Custom("List");
    public static NodeType ListItem { get; } = NodeType.Custom("ListItem");
    public static NodeType ThematicBreak { get; } = NodeType.Custom("ThematicBreak");
    public static NodeType HtmlBlock { get; } = NodeType.Custom("HtmlBlock");

    // Inline node types
    public static NodeType Emphasis { get; } = NodeType.Custom("Emphasis");
    public static NodeType StrongEmphasis { get; } = NodeType.Custom("StrongEmphasis");
    public static NodeType Link { get; } = NodeType.Custom("Link");
    public static NodeType Image { get; } = NodeType.Custom("Image");
    public static NodeType CodeSpan { get; } = NodeType.Custom("CodeSpan");
    public static NodeType HardLineBreak { get; } = NodeType.Custom("HardLineBreak");
    public static NodeType SoftLineBreak { get; } = NodeType.Custom("SoftLineBreak");
}