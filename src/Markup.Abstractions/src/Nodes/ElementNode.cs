using Femur.Parsing.Nodes;

namespace Femur.Markup.Abstractions.Nodes;

/// <summary>
/// Element node (e.g., &lt;div&gt;, &lt;p&gt;, etc.)
/// Can contain child nodes unless it's a void element or self-closing.
/// </summary>
public class ElementNode : ContainerNode
{
    public override NodeType NodeType => MarkupNodeType.Element;

    /// <summary>
    /// The tag name (e.g., "div", "p", "span")
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    private Dictionary<string, string>? _attributes;

    /// <summary>
    /// Attributes of the element.
    /// Dictionary is lazily initialized to reduce allocations for elements without attributes.
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
    /// Returns true if this element has any attributes
    /// </summary>
    public bool HasAttributes => this._attributes != null && this._attributes.Count > 0;

    /// <summary>
    /// Whether this is a self-closing tag (e.g., &lt;br /&gt;)
    /// Self-closing elements should not have children.
    /// </summary>
    public bool IsSelfClosing { get; set; }

    /// <summary>
    /// Whether this is a void element that cannot have children (e.g., &lt;img&gt;, &lt;br&gt;)
    /// Void elements should not have children.
    /// </summary>
    public bool IsVoidElement { get; set; }

    /// <summary>
    /// Returns true if this element can have children.
    /// Void elements and self-closing elements cannot have children.
    /// </summary>
    public bool CanHaveChildren => !this.IsVoidElement && !this.IsSelfClosing;
}