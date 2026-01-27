using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Tokenizer;

namespace ChtmlCompiler;

/// <summary>
/// Collects and manages scripts from template files.
/// </summary>
public static class ScriptCollector
{
    /// <summary>
    /// Collects all bottom scripts from the document AST recursively.
    /// </summary>
    public static List<ScriptNode> Collect(DocumentNode document)
    {
        var scripts = new List<ScriptNode>();
        CollectRecursive(document, scripts);
        return scripts;
    }

    /// <summary>
    /// Recursively collects scripts from a node and its children.
    /// </summary>
    private static void CollectRecursive(HtmlNode node, List<ScriptNode> scripts)
    {
        if (node is ScriptNode script && script.IsBottomScript)
        {
            scripts.Add(script);
        }

        foreach (var child in node.Children)
        {
            CollectRecursive(child, scripts);
        }
    }

    /// <summary>
    /// Generates a stable script ID from content hash.
    /// </summary>
    public static string GenerateId(string content)
    {
        // Generate a stable script ID from content hash
        // This ensures the same content always gets the same ID across compilations
        // Normalize content by trimming whitespace for consistent hashing
        var normalizedContent = content?.Trim() ?? string.Empty;
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedContent));
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return $"script-{hashString[..8]}";
        }
    }

    /// <summary>
    /// Escapes script content for verbatim string literals.
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
/// Generates code for script-related classes.
/// </summary>
public static class ScriptCodeGenerator
{
    /// <summary>
    /// Generates the ScriptRegistry class code.
    /// </summary>
    public static string GenerateRegistry(List<(string id, string content)> scripts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class ScriptRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    public static string GetScriptContent(string scriptId)");
        sb.AppendLine("    {");
        sb.AppendLine("        return scriptId switch");
        sb.AppendLine("        {");
        
        foreach (var (id, content) in scripts)
        {
            var escapedContent = ScriptCollector.EscapeContent(content);
            sb.AppendLine($"            \"{id}\" => @\"{escapedContent}\",");
        }
        
        sb.AppendLine("            _ => throw new System.ArgumentException($\"Unknown script: {scriptId}\", nameof(scriptId))");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates the ScriptRouteRegistration class code.
    /// </summary>
    public static string GenerateRouteRegistration(List<(string id, string content)> scripts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using Microsoft.AspNetCore.Http;");
        sb.AppendLine("using Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine("namespace Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class ScriptRouteRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterScriptRoutes(WebApplication app)");
        sb.AppendLine("    {");
        
        foreach (var (id, content) in scripts)
        {
            sb.AppendLine($"        app.MapGet(\"/script/{id}\", async (HttpContext ctx) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            ctx.Response.ContentType = \"application/javascript; charset=utf-8\";");
            sb.AppendLine($"            var scriptContent = ScriptRegistry.GetScriptContent(\"{id}\");");
            sb.AppendLine("            await ctx.Response.WriteAsync(scriptContent);");
            sb.AppendLine("        });");
        }
        
        var scriptCount = scripts.Count;
        if (scriptCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"        Console.WriteLine($\"✅ Registered {scriptCount} script routes\");");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
}

