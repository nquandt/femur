using Femur.Parsing.Nodes;

namespace Femur.Markdown.Abstractions.Nodes;

/// <summary>
/// Markdown document node.
/// Root node for a Markdown document.
/// </summary>
public class MarkdownDocumentNode : MarkdownContainerNode
{
    public override NodeType NodeType => MarkdownNodeType.Document;

    // Markdown-specific document properties can be added here if needed
    // For example: front matter, metadata, etc.
}