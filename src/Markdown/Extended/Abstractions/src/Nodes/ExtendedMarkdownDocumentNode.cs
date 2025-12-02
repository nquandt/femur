using Femur.Markdown.Abstractions.Nodes;
using Femur.Parsing.Nodes;

namespace Femur.Markdown.Extended.Abstractions.Nodes;

/// <summary>
/// Extended Markdown document node with YAML frontmatter support.
/// Extends the base MarkdownDocumentNode to include a frontmatter block node.
/// </summary>
public class ExtendedMarkdownDocumentNode : MarkdownDocumentNode
{
    /// <summary>
    /// The frontmatter block node, if present.
    /// When frontmatter exists in the document, this contains the parsed FrontMatterBlockNode.
    /// This node is also added as the first child in the Children collection.
    /// </summary>
    public FrontMatterBlockNode? FrontMatterBlock { get; set; }

    public override NodeType NodeType => ExtendedMarkdownNodeType.ExtendedDocument;
}
