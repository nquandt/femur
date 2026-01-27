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
/// Generates props classes for markdown files in _content folders.
/// Each _content folder gets a props class based on the frontmatter of all markdown files in that folder.
/// </summary>
public static class ContentPropsGenerator
{
    /// <summary>
    /// Generates a props class for a _content folder.
    /// Analyzes all markdown files in the folder and creates a props class with all frontmatter fields.
    /// </summary>
    /// <param name="contentFolderPath">Path to the _content folder</param>
    /// <param name="templatesRoot">Root directory of templates</param>
    /// <param name="className">Name for the generated props class</param>
    /// <param name="namespace">Namespace for the generated class</param>
    /// <returns>Generated C# code</returns>
    public static string Generate(string contentFolderPath, string templatesRoot, string className, string @namespace)
    {
        if (!Directory.Exists(contentFolderPath))
        {
            return GenerateStub(className, @namespace);
        }

        // Find all markdown files in the _content folder (including subdirectories)
        var markdownFiles = Directory.GetFiles(contentFolderPath, "*.md", SearchOption.AllDirectories);
        
        if (markdownFiles.Length == 0)
        {
            return GenerateStub(className, @namespace);
        }

        // Process markdown pipeline
        var pipeline = new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .UseAdvancedExtensions()
            .Build();

        // Collect all frontmatter fields from all files
        var allFields = new Dictionary<string, (string type, bool isNullable)>(); // field name -> (type, isNullable)
        
        foreach (var markdownFile in markdownFiles)
        {
            var markdownContent = File.ReadAllText(markdownFile);
            var document = Markdig.Markdown.Parse(markdownContent, pipeline);
            var frontMatter = ExtractFrontMatter(document);
            
            if (frontMatter != null)
            {
                foreach (var (key, value) in frontMatter)
                {
                    var propName = ToPascalCase(key);
                    // Infer type from value (default to string)
                    var isEmpty = value == null || string.IsNullOrEmpty(value.ToString());
                    var propType = InferType(value ?? "");
                    // If empty, mark as nullable; also mark DateTime as nullable since dates can be empty
                    var isNullable = isEmpty || propType == "string" || propType == "System.DateTime";
                    
                    // If field already exists, keep the more specific type and track nullability
                    if (allFields.TryGetValue(propName, out var existing))
                    {
                        // Prefer non-string types, but if we have mixed types, default to string
                        if (existing.type == "string" && propType != "string")
                        {
                            allFields[propName] = (propType, isNullable || existing.isNullable);
                        }
                        else if (existing.type == propType)
                        {
                            // Same type - combine nullability (if either is nullable, result is nullable)
                            allFields[propName] = (propType, isNullable || existing.isNullable);
                        }
                        else if (existing.type != "string" && propType != "string" && existing.type != propType)
                        {
                            // Mixed non-string types - default to string
                            allFields[propName] = ("string", true);
                        }
                    }
                    else
                    {
                        allFields[propName] = (propType, isNullable);
                    }
                }
            }
        }

        // Generate the props class
        var sb = new StringBuilder();
        sb.AppendLine("using Shared.Meta;");
        sb.AppendLine("using Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine($"namespace {@namespace};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Props class for markdown content in {Path.GetFileName(contentFolderPath)} folder.");
        sb.AppendLine($"/// Front matter fields become props, and Body is a RenderPipe for the compiled HTML.");
        sb.AppendLine($"/// Generated from {markdownFiles.Length} markdown file(s).");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");
        
        // Generate properties for all frontmatter fields
        foreach (var (propName, (propType, isNullable)) in allFields.OrderBy(kvp => kvp.Key))
        {
            var nullableType = isNullable && propType != "string" ? $"{propType}?" : propType;
            sb.AppendLine($"    public required {nullableType} {propName} {{ get; set; }}");
        }
        
        // Always add Body as RenderPipe
        sb.AppendLine($"    public required RenderPipe<GlobalProps> Body {{ get; set; }}");
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates a stub props class when _content folder doesn't exist.
    /// </summary>
    private static string GenerateStub(string className, string @namespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Shared.Meta;");
        sb.AppendLine("using Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine($"namespace {@namespace};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Stub props class - _content folder not found at compile time.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");
        sb.AppendLine("    public required RenderPipe<GlobalProps> Body { get; set; }");
        sb.AppendLine("}");
        return sb.ToString();
    }
    
    /// <summary>
    /// Infers the C# type from a YAML value.
    /// More conservative - defaults to string unless we're very confident.
    /// </summary>
    private static string InferType(object value)
    {
        if (value == null)
            return "string";
        
        var stringValue = value.ToString() ?? "";
        
        // If empty, default to string (can be nullable)
        if (string.IsNullOrEmpty(stringValue))
            return "string";
        
        // Only infer types for very clear cases
        // For now, default to string to match component prop expectations
        // Components typically expect strings which can be converted as needed
        
        return "string";
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
    /// Converts a key name to PascalCase for use as a property name.
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
}

