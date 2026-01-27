using Femur.Parsing.Nodes;

namespace Femur.Chtml.Parser;

/// <summary>
/// CHTML-specific node types.
/// These extend the standard HTML node types defined in HtmlParser.
/// </summary>
public static class ChtmlNodeType
{
    /// <summary>
    /// CHTML-specific node types
    /// </summary>
    public static NodeType Code { get; } = NodeType.Custom("Code");
    public static NodeType Component { get; } = NodeType.Custom("Component");
    public static NodeType Script { get; } = NodeType.Custom("Script");
    public static NodeType Style { get; } = NodeType.Custom("Style");
    public static NodeType If { get; } = NodeType.Custom("If");
    public static NodeType For { get; } = NodeType.Custom("For");
}
