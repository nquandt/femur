using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Tokenizer;

namespace ChtmlCompiler;

/// <summary>
/// Collects and manages styles from template files.
/// </summary>
public static class StyleCollector
{
    /// <summary>
    /// Collects all bottom styles from the document AST recursively.
    /// </summary>
    public static List<StyleNode> Collect(DocumentNode document)
    {
        var styles = new List<StyleNode>();
        CollectRecursive(document, styles);
        return styles;
    }

    /// <summary>
    /// Recursively collects styles from a node and its children.
    /// </summary>
    private static void CollectRecursive(HtmlNode node, List<StyleNode> styles)
    {
        if (node is StyleNode style && style.IsBottomStyle)
        {
            styles.Add(style);
        }

        foreach (var child in node.Children)
        {
            CollectRecursive(child, styles);
        }
    }

    /// <summary>
    /// Generates a stable style ID from content hash.
    /// </summary>
    public static string GenerateId(string content)
    {
        // Generate a stable style ID from content hash
        // This ensures the same content always gets the same ID across compilations
        // Normalize content by trimming whitespace for consistent hashing
        var normalizedContent = content?.Trim() ?? string.Empty;
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedContent));
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return $"style-{hashString[..8]}";
        }
    }

    /// <summary>
    /// Escapes style content for verbatim string literals.
    /// In C# verbatim strings (@""), quotes are escaped by doubling them.
    /// </summary>
    public static string EscapeContent(string content)
    {
        // Escape quotes for verbatim strings: " becomes ""
        // Normalize line endings to \n
        return content
            .Replace("\"", "\"\"")
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
    }
}

/// <summary>
/// Generates code for style-related classes.
/// </summary>
public static class StyleCodeGenerator
{
    /// <summary>
    /// Generates the StyleRegistry class code.
    /// </summary>
    public static string GenerateRegistry(List<(string id, string content)> styles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class StyleRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    public static string GetStyleContent(string styleId)");
        sb.AppendLine("    {");
        sb.AppendLine("        return styleId switch");
        sb.AppendLine("        {");
        
        foreach (var (id, content) in styles)
        {
            var escapedContent = StyleCollector.EscapeContent(content);
            sb.AppendLine($"            \"{id}\" => @\"{escapedContent}\",");
        }
        
        sb.AppendLine("            _ => throw new System.ArgumentException($\"Unknown style: {styleId}\", nameof(styleId))");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates the StyleRouteRegistration class code.
    /// </summary>
    public static string GenerateRouteRegistration(List<(string id, string content)> styles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using Microsoft.AspNetCore.Http;");
        sb.AppendLine("using Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine("namespace Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class StyleRouteRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterStyleRoutes(WebApplication app)");
        sb.AppendLine("    {");
        
        foreach (var (id, content) in styles)
        {
            sb.AppendLine($"        app.MapGet(\"/style/{id}\", async (HttpContext ctx) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            ctx.Response.ContentType = \"text/css; charset=utf-8\";");
            sb.AppendLine($"            var styleContent = StyleRegistry.GetStyleContent(\"{id}\");");
            sb.AppendLine("            await ctx.Response.WriteAsync(styleContent);");
            sb.AppendLine("        });");
        }
        
        var styleCount = styles.Count;
        if (styleCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"        Console.WriteLine($\"✅ Registered {styleCount} style routes\");");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
}

