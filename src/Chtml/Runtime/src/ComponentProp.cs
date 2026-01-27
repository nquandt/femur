using System;

namespace Femur.Chtml.Runtime;


/// <summary>
/// Represents a component prop with its type and nullability information.
/// </summary>
public class ComponentProp
{
    /// <summary>
    /// The name of the prop (e.g., "Title").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The C# type of the prop (e.g., "System.String").
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Whether this prop is nullable (optional). True if the type ends with "| null".
    /// </summary>
    public bool IsNullable { get; }

    public ComponentProp(string name, string type, bool isNullable)
    {
        this.Name = name;
        this.Type = type;
        this.IsNullable = isNullable;
    }

    /// <summary>
    /// Parses a prop definition string (e.g., "Title: System.String | null").
    /// </summary>
    public static ComponentProp Parse(string name, string typeDefinition)
    {
        var isNullable = typeDefinition.Contains("| null", StringComparison.OrdinalIgnoreCase);
        var type = typeDefinition.Replace("| null", "", StringComparison.OrdinalIgnoreCase).Trim();

        return new ComponentProp(name, type, isNullable);
    }
}

