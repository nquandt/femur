using System.Text;

namespace Femur.Chtml.CSharpRenderer;

/// <summary>
/// Generates code for the GlobalProps class.
/// </summary>
public static class GlobalPropsCodeGenerator
{
    /// <summary>
    /// Generates the GlobalProps class code from parsed props.
    /// </summary>
    public static string Generate(List<(string name, string type, bool isNullable)> props)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Global properties available to all templates during rendering.");
        sb.AppendLine("/// These properties are set at the request level and available throughout the render tree.");
        sb.AppendLine("/// Generated from global.chtml");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class GlobalProps");
        sb.AppendLine("{");

        if (props.Count == 0)
        {
            sb.AppendLine("    // Add global properties here as needed");
            sb.AppendLine("    // Example: public string? Language { get; set; }");
        }
        else
        {
            foreach (var (name, type, isNullable) in props)
            {
                var propName = StringUtils.ToPascalCase(name);
                var typeStr = type;
                var requiredKeyword = "";
                if (isNullable && !typeStr.EndsWith('?'))
                {
                    typeStr += "?";
                }
                else if (!isNullable)
                {
                    requiredKeyword = "required ";
                }

                sb.AppendLine($"    public {requiredKeyword}{typeStr} {propName} {{ get; set; }}");
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }
}



