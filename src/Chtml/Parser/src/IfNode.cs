using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;

namespace Femur.Chtml.Parser;

/// <summary>
/// If directive node ({#if condition}...{/if}) - can have children
/// Represents a conditional rendering block.
/// CHTML-specific feature.
/// </summary>
public class IfNode : ContainerNode
{
    public override NodeType NodeType => ChtmlNodeType.If;

    /// <summary>
    /// The condition expression (e.g., "props.IsActive", "vars.Count > 0")
    /// </summary>
    public string Condition { get; set; } = string.Empty;
}
