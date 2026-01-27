using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using YamlDotNet.RepresentationModel;

namespace ChtmlCompiler;

/// <summary>
/// Generates static classes for markdown files referenced via LoadMarkdown.
/// Each markdown file gets a static class with front matter as static fields and compiled HTML as a RenderPipe.
/// </summary>
public static class MarkdownGenerator
{
    /// <summary>
    /// Generates a static class for a markdown file.
    /// </summary>
    /// <param name="markdownFilePath">Path to the markdown file</param>
    /// <param name="templatesRoot">Root directory of templates</param>
    /// <param name="className">Name for the generated class</param>
    /// <param name="namespace">Namespace for the generated class</param>
    /// <returns>Generated C# code</returns>
    public static string Generate(string markdownFilePath, string templatesRoot, string className, string @namespace)
    {
        if (!File.Exists(markdownFilePath))
        {
            // Return a stub class if file doesn't exist
            return GenerateStub(className, @namespace);
        }

        var markdownContent = File.ReadAllText(markdownFilePath);
        
        // Process markdown with Markdig
        var pipeline = new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .UseAdvancedExtensions()
            .Build();
        
        // Parse markdown document
        var document = Markdig.Markdown.Parse(markdownContent, pipeline);
        
        // Extract front matter
        var frontMatter = ExtractFrontMatter(document);
        
        // Extract markdown body (everything after front matter)
        var markdownBody = ExtractMarkdownBody(markdownContent);
        
        // Convert markdown body to HTML
        var htmlContent = document.ToHtml(pipeline);
        
        // Generate the static class
        var sb = new StringBuilder();
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Shared.Meta;");
        sb.AppendLine("using Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine($"namespace {@namespace};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Static class for markdown file: {Path.GetFileName(markdownFilePath)}");
        sb.AppendLine($"/// Generated at compile time from markdown content.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public static class {className}");
        sb.AppendLine("{");
        
        // Generate public static fields for front matter
        if (frontMatter != null && frontMatter.Count > 0)
        {
            foreach (var (key, value) in frontMatter)
            {
                var fieldName = ToPascalCase(key);
                var fieldValue = FormatValue(value);
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Front matter field: {key}");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    public static string {fieldName} = {fieldValue};");
            }
            sb.AppendLine();
        }
        
        // Generate static RenderAsync method with compiled HTML
        var escapedHtml = EscapeStringForVerbatim(htmlContent);
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Renders the compiled HTML from the markdown body.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static async ValueTask RenderAsync(RenderContext<GlobalProps> renderContext)");
        sb.AppendLine("    {");
        sb.AppendLine("        var (writer, globalProps) = renderContext;");
        sb.AppendLine($"        await writer.WriteAsync(@\"{escapedHtml}\");");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates a stub class when markdown file doesn't exist.
    /// </summary>
    private static string GenerateStub(string className, string @namespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Shared.Meta;");
        sb.AppendLine("using Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine($"namespace {@namespace};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Stub class - markdown file not found at compile time.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public static class {className}");
        sb.AppendLine("{");
        sb.AppendLine("    public static async ValueTask RenderAsync(RenderContext<GlobalProps> renderContext)");
        sb.AppendLine("    {");
        sb.AppendLine("        var (writer, globalProps) = renderContext;");
        sb.AppendLine("        await writer.WriteAsync(\"<!-- Error: Markdown file not found -->\");");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
    
    /// <summary>
    /// Extracts YAML front matter from a Markdig document.
    /// </summary>
    private static Dictionary<string, object>? ExtractFrontMatter(Markdig.Syntax.MarkdownDocument document)
    {
        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (yamlBlock == null) return null;
        
        var yamlLines = new StringBuilder();
        foreach (var line in yamlBlock.Lines.Lines)
        {
            if (line.Slice.Text != null)
            {
                yamlLines.AppendLine(line.Slice.ToString());
            }
        }
        
        var yamlText = yamlLines.ToString();
        return ParseYaml(yamlText);
    }
    
    /// <summary>
    /// Extracts markdown body (everything after front matter).
    /// </summary>
    private static string ExtractMarkdownBody(string markdownContent)
    {
        // Find the end of front matter (---)
        var lines = markdownContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var startIndex = 0;
        
        // Skip first ---
        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            startIndex = 1;
            // Find second ---
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "---")
                {
                    startIndex = i + 1;
                    break;
                }
            }
        }
        
        // Join remaining lines
        return string.Join(Environment.NewLine, lines.Skip(startIndex));
    }
    
    /// <summary>
    /// Parses YAML text into a dictionary.
    /// </summary>
    private static Dictionary<string, object>? ParseYaml(string? yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText)) return null;
        
        var dict = new Dictionary<string, object>();
        var yaml = new YamlStream();
        yaml.Load(new StringReader(yamlText));
        
        if (yaml.Documents.Count == 0) return dict;
        
        var root = yaml.Documents[0].RootNode as YamlMappingNode;
        if (root == null) return dict;
        
        foreach (var entry in root.Children)
        {
            var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
            var value = entry.Value switch
            {
                YamlScalarNode s => s.Value ?? string.Empty,
                _ => entry.Value.ToString() ?? string.Empty
            };
            dict[key] = value;
        }
        
        return dict;
    }
    
    /// <summary>
    /// Converts a key name to PascalCase for use as a field name.
    /// </summary>
    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        // Split by common separators and capitalize each part
        var parts = input.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();
        
        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                result.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    result.Append(part.Substring(1));
                }
            }
        }
        
        return result.ToString();
    }
    
    /// <summary>
    /// Formats a value for use as a C# string literal.
    /// </summary>
    private static string FormatValue(object value)
    {
        if (value == null)
            return "null";
        
        var stringValue = value.ToString() ?? "";
        
        // Escape for verbatim string literal
        var escaped = EscapeStringForVerbatim(stringValue);
        return $"@\"{escaped}\"";
    }
    
    /// <summary>
    /// Escapes a string for use in a verbatim string literal.
    /// </summary>
    private static string EscapeStringForVerbatim(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        // For verbatim strings, we only need to escape quotes by doubling them
        return input.Replace("\"", "\"\"");
    }
}

