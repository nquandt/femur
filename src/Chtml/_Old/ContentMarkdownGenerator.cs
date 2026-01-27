using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Tokenizer;
using YamlDotNet.RepresentationModel;

namespace ChtmlCompiler;

/// <summary>
/// Generates static classes for markdown files in _content folders.
/// Each markdown file gets a static class that returns a ContentProps instance with compiled data.
/// </summary>
public static class ContentMarkdownGenerator
{
    /// <summary>
    /// Generates a static class for a markdown file that returns a ContentProps instance.
    /// </summary>
    /// <param name="markdownFilePath">Path to the markdown file</param>
    /// <param name="templatesRoot">Root directory of templates</param>
    /// <param name="className">Name for the generated class (e.g., "En", "Es")</param>
    /// <param name="namespace">Namespace for the generated class</param>
    /// <param name="propsTypeName">The ContentProps type name (e.g., "Templates.Generated.Pages.ContentProps")</param>
    /// <returns>Generated C# code</returns>
    public static string Generate(string markdownFilePath, string templatesRoot, string className, string @namespace, string propsTypeName)
    {
        if (!File.Exists(markdownFilePath))
        {
            return GenerateStub(className, @namespace, propsTypeName);
        }

        var markdownContent = File.ReadAllText(markdownFilePath);
        
        // Process markdown with Markdig
        // IMPORTANT: CustomContainers must be enabled BEFORE GenericAttributes for attributes to work
        // See: https://github.com/xoofx/markdig#custom-container
        var pipeline = new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .UseAdvancedExtensions() // This includes UseCustomContainers
            .UseCustomContainers() // Explicitly enable CustomContainers (ensures it's before GenericAttributes)
            .UseGenericAttributes() // Enable Generic Attributes extension for {attr="value"} syntax (must be AFTER CustomContainers)
            .Build();
        
        // Parse markdown document
        var document = Markdig.Markdown.Parse(markdownContent, pipeline);
        
        // Extract front matter
        var frontMatter = ExtractFrontMatter(document);
        
        // Extract markdown body (everything after front matter)
        var markdownBody = ExtractMarkdownBody(markdownContent);
        
        // Calculate offset where markdownBody starts in markdownContent
        // This is needed because Span positions in the parsed document are relative to markdownContent,
        // but we pass markdownBody to ConvertToChtml
        var bodyStartOffset = markdownContent.Length - markdownBody.Length;
        if (markdownContent.StartsWith("---"))
        {
            var firstDashIndex = markdownContent.IndexOf("---", 3);
            if (firstDashIndex >= 0)
            {
                // Find the newline after the second ---
                var afterDashIndex = markdownContent.IndexOf('\n', firstDashIndex + 3);
                if (afterDashIndex >= 0)
                {
                    bodyStartOffset = afterDashIndex + 1;
                    // Handle \r\n
                    if (bodyStartOffset < markdownContent.Length && markdownContent[bodyStartOffset] == '\r')
                    {
                        bodyStartOffset++;
                    }
                }
            }
        }
        
        // Convert markdown body to chtml (transforms ComponentName containers to component tags)
        // Pass the bodyStartOffset so the renderer can adjust Span positions
        var chtmlContent = MarkdownToChtmlConverter.ConvertToChtml(markdownBody, pipeline, bodyStartOffset);
        
        // Parse chtml content to AST
        var chtmlBytes = Encoding.UTF8.GetBytes(chtmlContent);
        var chtmlDocument = ChtmlParser.Parse(chtmlBytes);
        
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
        sb.AppendLine($"/// Returns a {propsTypeName} instance with front matter fields and Body RenderPipe.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public static class {className}");
        sb.AppendLine("{");
        
        // Generate static method that returns ContentProps instance
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates and returns a ContentProps instance with front matter fields and Body RenderPipe.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static {propsTypeName} GetContent()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {propsTypeName}");
        sb.AppendLine("        {");
        
        // Generate property assignments for front matter fields
        if (frontMatter != null && frontMatter.Count > 0)
        {
            foreach (var (key, value) in frontMatter)
            {
                var propName = ToPascalCase(key);
                var propValue = FormatValue(value);
                sb.AppendLine($"            {propName} = {propValue},");
            }
        }
        
        // Generate Body RenderPipe with compiled chtml rendering code
        sb.AppendLine("            Body = new RenderPipe<GlobalProps>(async ctx =>");
        sb.AppendLine("            {");
        sb.AppendLine("                var writer = ctx.Writer;");
        
        // Generate rendering code for the chtml document
        GenerateBodyRendering(sb, chtmlDocument, 4, templatesRoot);
        
        sb.AppendLine("            })");
        
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates a stub class when markdown file doesn't exist.
    /// </summary>
    private static string GenerateStub(string className, string @namespace, string propsTypeName)
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
        sb.AppendLine($"    public static {propsTypeName} GetContent()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {propsTypeName}");
        sb.AppendLine("        {");
        sb.AppendLine("            Body = new RenderPipe<GlobalProps>(async ctx =>");
        sb.AppendLine("            {");
        sb.AppendLine("                await ctx.Writer.WriteAsync(\"<!-- Error: Markdown file not found -->\");");
        sb.AppendLine("            })");
        sb.AppendLine("        };");
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
    
    /// <summary>
    /// Formats a value for use as a C# string literal.
    /// Since ContentProps uses string types, always format as string.
    /// </summary>
    private static string FormatValue(object value)
    {
        if (value == null)
            return "null";
        
        var stringValue = value.ToString() ?? "";
        
        // If empty, return empty string literal
        if (string.IsNullOrEmpty(stringValue))
            return "@\"\"";
        
        // Format as string literal
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
    
    /// <summary>
    /// Generates rendering code for the markdown body from a parsed chtml document.
    /// This generates code that renders the AST nodes, including component calls.
    /// </summary>
    private static void GenerateBodyRendering(StringBuilder sb, DocumentNode document, int indentLevel, string templatesRoot)
    {
        var indent = new string(' ', indentLevel);
        
        foreach (var child in document.Children)
        {
            GenerateNodeRendering(sb, child, indentLevel, templatesRoot);
        }
    }
    
    /// <summary>
    /// Generates rendering code for a single AST node.
    /// </summary>
    private static void GenerateNodeRendering(StringBuilder sb, HtmlNode node, int indentLevel, string templatesRoot)
    {
        var indent = new string(' ', indentLevel);
        
        switch (node)
        {
            case ElementNode element:
                GenerateElementRendering(sb, element, indentLevel, templatesRoot);
                break;
                
            case TextNode text:
                var textContent = EscapeStringForVerbatim(text.Content);
                sb.AppendLine($"{indent}await writer.WriteAsync(@\"{textContent}\");");
                break;
                
            case ComponentNode component:
                GenerateComponentRendering(sb, component, indentLevel, templatesRoot);
                break;
                
            case CommentNode comment:
                var commentContent = EscapeStringForVerbatim(comment.Content);
                sb.AppendLine($"{indent}await writer.WriteAsync($\"<!--{commentContent}-->\");");
                break;
                
            case CodeNode code:
                // Code nodes in markdown body should be rendered as-is
                var codeContent = EscapeStringForVerbatim(code.Content);
                sb.AppendLine($"{indent}await writer.WriteAsync(@\"{codeContent}\");");
                break;
                
            default:
                // For other node types, try to render as HTML string
                var htmlContent = EscapeStringForVerbatim(node.ToString() ?? "");
                if (!string.IsNullOrEmpty(htmlContent))
                {
                    sb.AppendLine($"{indent}await writer.WriteAsync(@\"{htmlContent}\");");
                }
                break;
        }
    }
    
    /// <summary>
    /// Generates rendering code for an HTML element node.
    /// </summary>
    private static void GenerateElementRendering(StringBuilder sb, ElementNode element, int indentLevel, string templatesRoot)
    {
        var indent = new string(' ', indentLevel);
        
        // Write opening tag
        sb.Append($"{indent}await writer.WriteAsync(\"<{element.TagName}");
        
        // Write attributes
        foreach (var (key, value) in element.Attributes)
        {
            var escapedValue = value.Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
            sb.Append($" {key}=\\\"{escapedValue}\\\"");
        }
        
        sb.AppendLine(">\");");
        
        // Render children
        foreach (var child in element.Children)
        {
            GenerateNodeRendering(sb, child, indentLevel, templatesRoot);
        }
        
        // Write closing tag
        sb.AppendLine($"{indent}await writer.WriteAsync(\"</{element.TagName}>\");");
    }
    
    /// <summary>
    /// Generates rendering code for a component node.
    /// Components are rendered by calling their RenderAsync method with props.
    /// </summary>
    private static void GenerateComponentRendering(StringBuilder sb, ComponentNode component, int indentLevel, string templatesRoot)
    {
        var indent = new string(' ', indentLevel);
        var componentName = component.ComponentName;
        var hasChildren = component.Children.Any();
        var hasAttributes = component.Attributes.Any();
        
        // Resolve component namespace (assume Components namespace for now)
        // Component files are typically named index.chtml, so the class is usually "Index"
        // For components with GlobalAs, the component name matches, but class is still "Index"
        var componentNamespace = $"Templates.Generated.Components.{componentName}";
        var componentClassName = "Index"; // Components are typically in index.chtml files
        var fullComponentPath = $"{componentNamespace}.{componentClassName}";
        
        // Build component call
        sb.AppendLine($"{indent}await {fullComponentPath}.RenderAsync(");
        sb.AppendLine($"{indent}    renderContext: ctx,");
        
        // Generate props instance from attributes and content
        // Components with computed props use InputProps, others use Props
        // Codeblock now has ComputedProps, so it must use IndexInputProps
        var componentBaseName = componentClassName; // e.g., "Index"
        var inputPropsTypeName = $"{componentNamespace}.{componentBaseName}InputProps";
        var propsTypeName = $"{componentNamespace}.{componentBaseName}Props";
        // Codeblock has ComputedProps, so use InputProps (TransformProps will compute Content)
        var propsTypeToUse = componentName.Equals("Codeblock", StringComparison.OrdinalIgnoreCase) 
            ? inputPropsTypeName 
            : propsTypeName;
        sb.AppendLine($"{indent}    inputProps: new {propsTypeToUse}");
        sb.AppendLine($"{indent}    {{");
        
        // Track props to add
        var props = new List<string>();
        
        // Special handling for Codeblock component with rawUrl starting with ~/
        // Load file content at compile time and process with Shiki
        if (componentName.Equals("Codeblock", StringComparison.OrdinalIgnoreCase) &&
            component.Attributes.TryGetValue("rawUrl", out var rawUrlValue) &&
            !string.IsNullOrWhiteSpace(rawUrlValue) &&
            rawUrlValue.StartsWith("~/", StringComparison.Ordinal))
        {
            // Extract language from attributes
            string? language = null;
            if (component.Attributes.TryGetValue("lang", out var langValue))
            {
                language = langValue;
            }
            language ??= "text";
            
            // Resolve ~/ path to wwwroot
            // ~/files/snippets/file.cs -> wwwroot/files/snippets/file.cs
            var wwwrootPath = rawUrlValue.Substring(2); // Remove ~/
            var wwwrootDir = Path.Combine(Path.GetDirectoryName(templatesRoot) ?? templatesRoot, "wwwroot");
            var filePath = Path.Combine(wwwrootDir, wwwrootPath.Replace('/', Path.DirectorySeparatorChar));
            
            // Load file content at compile time
            string? fileContent = null;
            if (File.Exists(filePath))
            {
                try
                {
                    fileContent = File.ReadAllText(filePath);
                }
                catch (Exception ex)
                {
                    // If file loading fails, generate error comment
                    var errorMsg = EscapeStringForVerbatim($"<!-- Error loading file {rawUrlValue}: {ex.Message} -->");
                    props.Add($"Content = @\"{errorMsg}\"");
                }
            }
            else
            {
                // File not found - generate error comment
                var errorMsg = EscapeStringForVerbatim($"<!-- Error: File not found: {filePath} -->");
                props.Add($"Content = @\"{errorMsg}\"");
            }
            
            // Process loaded content with Shiki
            if (fileContent != null)
            {
                var highlightedHtml = ShikiProcessor.HighlightCode(fileContent, language);
                
                if (highlightedHtml != null)
                {
                    // Shiki succeeded - pass highlighted HTML as Content prop
                    var escapedHtml = EscapeStringForVerbatim(highlightedHtml);
                    props.Add($"Content = @\"{escapedHtml}\"");
                }
                else
                {
                    // Shiki failed - fallback to plain code, still pass as Content
                    var escapedCode = EscapeStringForVerbatim(System.Net.WebUtility.HtmlEncode(fileContent));
                    var languageClass = $"language-{language}";
                    var languageClassEscaped = EscapeStringForVerbatim(languageClass);
                    var htmlContent = $"<pre><code class=\"{languageClassEscaped}\">{escapedCode}</code></pre>";
                    var escapedHtml = EscapeStringForVerbatim(htmlContent);
                    props.Add($"Content = @\"{escapedHtml}\"");
                }
            }
            
            // Convert ~/ to / for static file URL (e.g., ~/files/snippets/file.cs -> /files/snippets/file.cs)
            // This allows the download button to work with static file resolver
            var staticFileUrl = "/" + wwwrootPath;
            var escapedRawUrl = EscapeStringForVerbatim(staticFileUrl);
            props.Add($"RawUrl = @\"{escapedRawUrl}\"");
        }
        // Check if Content attribute exists (from ComponentName containers that pass content as attribute)
        else if (component.Attributes.ContainsKey("Content"))
        {
            var contentValue = component.Attributes["Content"];
            // Content is already escaped for HTML attribute, unescape it for C# verbatim string
            var unescapedContent = contentValue
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&");
            
            // Special handling for Codeblock component - process with Shiki at compile time
            if (componentName.Equals("Codeblock", StringComparison.OrdinalIgnoreCase))
            {
                // Extract language from attributes
                string? language = null;
                if (component.Attributes.TryGetValue("lang", out var langValue))
                {
                    language = langValue;
                }
                language ??= "text";
                
                // Strip triple backticks if present
                var code = unescapedContent;
                code = System.Text.RegularExpressions.Regex.Replace(
                    code,
                    @"^```\w*\s*\r?\n?",
                    "",
                    System.Text.RegularExpressions.RegexOptions.Multiline
                );
                code = System.Text.RegularExpressions.Regex.Replace(
                    code,
                    @"\r?\n?```\s*$",
                    "",
                    System.Text.RegularExpressions.RegexOptions.Multiline
                );
                code = code.Trim();
                
                // If code is empty after trimming, don't set Content (let TransformProps handle file loading)
                if (string.IsNullOrWhiteSpace(code))
                {
                    // Don't add Content prop - TransformProps will handle loading from RawUrl if needed
                }
                else
                {
                    // Process with Shiki during compilation
                    var highlightedHtml = ShikiProcessor.HighlightCode(code, language);
                    
                    if (highlightedHtml != null)
                    {
                        // Shiki succeeded - pass highlighted HTML as Content prop
                        var escapedHtml = EscapeStringForVerbatim(highlightedHtml);
                        props.Add($"Content = @\"{escapedHtml}\"");
                    }
                    else
                    {
                        // Shiki failed - fallback to plain code, still pass as Content
                        var escapedCode = EscapeStringForVerbatim(System.Net.WebUtility.HtmlEncode(code));
                        var languageClass = $"language-{language}";
                        var languageClassEscaped = EscapeStringForVerbatim(languageClass);
                        var htmlContent = $"<pre><code class=\"{languageClassEscaped}\">{escapedCode}</code></pre>";
                        var escapedHtml = EscapeStringForVerbatim(htmlContent);
                        props.Add($"Content = @\"{escapedHtml}\"");
                    }
                }
            }
            else
            {
                // For other components, Content attribute maps to Content prop
                var escapedContent = EscapeStringForVerbatim(unescapedContent);
                props.Add($"Content = @\"{escapedContent}\"");
            }
        }
        // If component has children but no Content attribute, add them as Content prop
        else if (hasChildren)
        {
            // Collect content from children - preserve HTML structure
            var contentBuilder = new StringBuilder();
            CollectHtmlContent(component, contentBuilder);
            var content = contentBuilder.ToString().Trim();
            
            if (!string.IsNullOrEmpty(content))
            {
                var escapedContent = EscapeStringForVerbatim(content);
                // Pass as string (component expects System.String, not RenderPipe)
                props.Add($"Content = @\"{escapedContent}\"");
            }
        }
        
        // Add other attributes as props
        foreach (var attr in component.Attributes)
        {
            // Skip Content attribute - already handled above
            if (attr.Key.Equals("Content", StringComparison.OrdinalIgnoreCase))
                continue;
            
            // Skip rawUrl if it starts with ~/ (already loaded at compile time)
            if (attr.Key.Equals("rawUrl", StringComparison.OrdinalIgnoreCase) &&
                attr.Value.StartsWith("~/", StringComparison.Ordinal))
                continue;
                
            var propName = ToPascalCase(attr.Key);
            var escapedValue = EscapeStringForVerbatim(attr.Value);
            props.Add($"{propName} = @\"{escapedValue}\"");
        }
        
        // Write props
        for (int i = 0; i < props.Count; i++)
        {
            var isLast = i == props.Count - 1;
            sb.AppendLine($"{indent}        {props[i]}{(isLast ? "" : ",")}");
        }
        
        sb.AppendLine($"{indent}    }}");
        
        sb.AppendLine($"{indent});");
    }
    
    /// <summary>
    /// Collects HTML content from a node and its children, preserving HTML structure.
    /// </summary>
    private static void CollectHtmlContent(HtmlNode node, StringBuilder output)
    {
        switch (node)
        {
            case TextNode textNode:
                output.Append(textNode.Content);
                break;
                
            case ElementNode elementNode:
                // Render element as HTML
                output.Append($"<{elementNode.TagName}");
                foreach (var (key, value) in elementNode.Attributes)
                {
                    var escapedValue = value.Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
                    output.Append($" {key}=\"{escapedValue}\"");
                }
                output.Append(">");
                
                foreach (var child in elementNode.Children)
                {
                    CollectHtmlContent(child, output);
                }
                
                output.Append($"</{elementNode.TagName}>");
                break;
                
            case ComponentNode componentNode:
                // For components within markdown content, render as HTML comment or skip
                // (components shouldn't be nested in markdown content typically)
                foreach (var child in componentNode.Children)
                {
                    CollectHtmlContent(child, output);
                }
                break;
        }
    }
}

