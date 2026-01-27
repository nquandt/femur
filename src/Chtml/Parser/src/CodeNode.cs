using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;

namespace Femur.Chtml.Parser;

/// <summary>
/// Code block node ({ ... }) - cannot have children
/// Represents code expressions embedded in HTML content.
/// CHTML-specific feature.
/// </summary>
public class CodeNode : LeafNode
{
    public override NodeType NodeType => ChtmlNodeType.Code;

    /// <summary>
    /// The code content between braces (without the braces themselves)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    // TODO: Future enhancement - parse the content into structured tokens/expressions
    // When implementing parsing, add:
    // - Parsed tokens/expressions
    // - Syntax tree for the code content
    // - Type information if applicable
}
