namespace Femur.Parsing.Nodes;

/// <summary>
/// Represents a node type identifier.
/// Uses a sealed class with static properties for standard HTML types.
/// Extensions (like CHTML) can create their own node types using string values.
/// </summary>
public sealed class NodeType
{
    private readonly string _value;

    private NodeType(string value)
    {
        this._value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Text node type - used by all parsers (Markdown, HTML, XML, CHTML)
    /// </summary>
    public static NodeType Text { get; } = new("Text");

    /// <summary>
    /// Creates a custom node type (for extensions like CHTML)
    /// </summary>
    public static NodeType Custom(string value) => new(value);

    public override string ToString() => this._value;
    public override bool Equals(object? obj) => obj is NodeType other && this._value == other._value;
    public override int GetHashCode() => this._value.GetHashCode();

    public static bool operator ==(NodeType? left, NodeType? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left._value == right._value;
    }

    public static bool operator !=(NodeType? left, NodeType? right) => !(left == right);
}