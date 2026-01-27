using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;

namespace Femur.Chtml.Parser;

/// <summary>
/// Component node (&lt;:ComponentName /&gt;) - can have children
/// Represents a component reference in the template system.
/// CHTML-specific feature.
/// </summary>
public class ComponentNode : ContainerNode
{
    public override NodeType NodeType => ChtmlNodeType.Component;

    /// <summary>
    /// The component name (e.g., "Header", "Layout", "Footer")
    /// </summary>
    public string ComponentName { get; set; } = string.Empty;

    private Dictionary<string, string>? _attributes;

    /// <summary>
    /// Attributes passed to the component.
    /// Dictionary is lazily initialized to reduce allocations for components without attributes.
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
    /// Returns true if this component has any attributes
    /// </summary>
    public bool HasAttributes => this._attributes != null && this._attributes.Count > 0;

    /// <summary>
    /// Whether this is a self-closing tag (e.g., &lt;:Header /&gt;)
    /// Self-closing components should not have children.
    /// </summary>
    public bool IsSelfClosing { get; set; }

    /// <summary>
    /// Returns true if this component can have children.
    /// Self-closing components cannot have children.
    /// </summary>
    public bool CanHaveChildren => !this.IsSelfClosing;
}
