using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChtmlCompiler;

/// <summary>
/// Generates code for global component registry.
/// Components marked with GlobalAs in frontmatter are registered globally.
/// </summary>
public static class GlobalComponentRegistryGenerator
{
    /// <summary>
    /// Generates the GlobalComponentRegistry class code.
    /// </summary>
    public static string GenerateRegistry(Dictionary<string, string> globalComponents)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Templates.Generated.Components;");
        sb.AppendLine();
        sb.AppendLine("namespace Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Global component registry. Maps global component names to their types.");
        sb.AppendLine("/// Components marked with GlobalAs in frontmatter are registered here.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GlobalComponentRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly Dictionary<string, Type> _globalComponents = new(StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("    {");
        
        foreach (var (globalName, className) in globalComponents.OrderBy(kvp => kvp.Key))
        {
            sb.AppendLine($"        [\"{globalName}\"] = typeof({className}),");
        }
        
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the type for a global component by name.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static Type? GetGlobalComponentType(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        return _globalComponents.TryGetValue(name, out var type) ? type : null;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Checks if a component name is registered globally.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool IsGlobalComponent(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        return _globalComponents.ContainsKey(name);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
}

