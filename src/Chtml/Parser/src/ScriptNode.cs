using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;

namespace Femur.Chtml.Parser;

/// <summary>
/// Script node (&lt;script&gt;...&lt;/script&gt;)
/// Represents a script tag. Bottom scripts (at end of content) are hoisted and rendered separately.
/// CHTML-specific feature (content extraction).
/// </summary>
public class ScriptNode : ContainerNode
{
    public override NodeType NodeType => ChtmlNodeType.Script;

    /// <summary>
    /// The tag name (always "script")
    /// </summary>
    public string TagName => "script";

    private Dictionary<string, string>? _attributes;

    /// <summary>
    /// Attributes of the script element.
    /// Dictionary is lazily initialized to reduce allocations for scripts without attributes.
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
    /// Returns true if this script has any attributes
    /// </summary>
    public bool HasAttributes => this._attributes != null && this._attributes.Count > 0;

    /// <summary>
    /// Whether this is a self-closing tag (e.g., &lt;script /&gt;)
    /// Self-closing script tags should not have children.
    /// </summary>
    public bool IsSelfClosing { get; set; }

    /// <summary>
    /// The script content (JavaScript code) - extracted from children when closing tag is processed
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a bottom script that should be hoisted
    /// Bottom scripts are at the end of the content and will be rendered via RenderScripts()
    /// </summary>
    public bool IsBottomScript { get; set; }

    /// <summary>
    /// The script ID (generated from content hash)
    /// Used for deduplication and route registration
    /// </summary>
    public string? ScriptId { get; set; }
}