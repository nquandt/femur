using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;

namespace Femur.Chtml.Parser;

/// <summary>
/// For directive node ({#for item in collection}...{/for}) - can have children
/// Represents an iteration/loop block.
/// CHTML-specific feature.
/// </summary>
public class ForNode : ContainerNode
{
    public override NodeType NodeType => ChtmlNodeType.For;

    /// <summary>
    /// The loop variable name (e.g., "item", "user")
    /// </summary>
    public string VariableName { get; set; } = string.Empty;

    /// <summary>
    /// The collection expression to iterate over (e.g., "props.Items", "vars.Users")
    /// </summary>
    public string CollectionExpression { get; set; } = string.Empty;
}
