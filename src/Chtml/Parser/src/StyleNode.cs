using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;

namespace Femur.Chtml.Parser;

/// <summary>
/// Style node (&lt;style&gt;...&lt;/style&gt;)
/// Represents a style tag. Bottom styles (at end of content) are hoisted and rendered separately.
/// CHTML-specific feature (content extraction).
/// </summary>
public class StyleNode : ContainerNode
{
    public override NodeType NodeType => ChtmlNodeType.Style;

    /// <summary>
    /// The tag name (always "style")
    /// </summary>
    public string TagName => "style";

    private Dictionary<string, string>? _attributes;

    /// <summary>
    /// Attributes of the style element.
    /// Dictionary is lazily initialized to reduce allocations for styles without attributes.
    /// </summary>
    public Dictionary<string, string> Attributes
    {
        get
        {
            this._attributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return this._attributes;
        }
    }

    /// <summary>
    /// Returns true if this style has any attributes
    /// </summary>
    public bool HasAttributes => this._attributes != null && this._attributes.Count > 0;

    /// <summary>
    /// Whether this is a self-closing tag (e.g., &lt;style /&gt;)
    /// Self-closing style tags should not have children.
    /// </summary>
    public bool IsSelfClosing { get; set; }

    /// <summary>
    /// The style content (CSS code) - extracted from children when closing tag is processed
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a bottom style that should be hoisted
    /// Bottom styles are at the end of the content and will be rendered via RenderStyles()
    /// </summary>
    public bool IsBottomStyle { get; set; }

    /// <summary>
    /// The style ID (generated from content hash)
    /// Used for deduplication and route registration
    /// </summary>
    public string? StyleId { get; set; }
}