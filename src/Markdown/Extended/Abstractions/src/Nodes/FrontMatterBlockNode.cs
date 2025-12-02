using Femur.Parsing.Nodes;

namespace Femur.Markdown.Extended.Abstractions.Nodes;

/// <summary>
/// Represents a YAML frontmatter block in an Extended Markdown document.
/// Frontmatter appears at the beginning of a document, bounded by --- delimiters,
/// and contains structured metadata as YAML key-value pairs.
/// </summary>
public class FrontMatterBlockNode : Node
{
    /// <summary>
    /// The raw YAML text content of the frontmatter block (without the --- delimiters).
    /// </summary>
    public string RawContent { get; set; } = string.Empty;

    /// <summary>
    /// Parsed frontmatter as a dictionary of key-value pairs.
    /// Keys are strings, values can be strings, lists, or nested dictionaries.
    /// Null if YAML parsing failed.
    /// </summary>
    public Dictionary<string, object>? ParsedData { get; set; }

    public override NodeType NodeType => ExtendedMarkdownNodeType.FrontMatterBlock;
}
