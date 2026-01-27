using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.GenericAttributes;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ChtmlCompiler;

/// <summary>
/// Converts markdown to chtml syntax, transforming ComponentName custom containers into chtml component tags.
/// Regular markdown is converted to HTML (which is valid chtml), and ComponentName containers become component tags.
/// </summary>
public static class MarkdownToChtmlConverter
{
    /// <summary>
    /// Converts markdown content to chtml syntax.
    /// </summary>
    /// <param name="markdownContent">The markdown content to convert</param>
    /// <param name="pipeline">The Markdig pipeline to use</param>
    /// <param name="sourceOffset">Offset to adjust Span positions (if markdownContent is a subset of the original)</param>
    public static string ConvertToChtml(string markdownContent, MarkdownPipeline pipeline, int sourceOffset = 0)
    {
        // Parse markdown document
        var document = Markdig.Markdown.Parse(markdownContent, pipeline);
        
        // Create a custom HTML renderer that handles ComponentName containers specially
        // Pass the original markdown content so we can extract raw source text
        var output = new StringWriter();
        var renderer = new ChtmlMarkdownRenderer(output, pipeline, markdownContent, sourceOffset);
        renderer.Render(document);
        
        return output.ToString();
    }
    
    /// <summary>
    /// Custom HTML renderer that transforms ComponentName containers into chtml component tags.
    /// </summary>
    private class ChtmlMarkdownRenderer : HtmlRenderer
    {
        private readonly MarkdownPipeline _pipeline;
        private readonly string _sourceMarkdown;
        private readonly int _sourceOffset; // Offset to adjust Span positions
        
        public ChtmlMarkdownRenderer(TextWriter writer, MarkdownPipeline pipeline, string sourceMarkdown, int sourceOffset = 0) : base(writer)
        {
            _pipeline = pipeline;
            _sourceMarkdown = sourceMarkdown;
            _sourceOffset = sourceOffset;
            // Replace the default custom container renderer with our custom one
            var defaultRenderer = ObjectRenderers.OfType<HtmlCustomContainerRenderer>().FirstOrDefault();
            if (defaultRenderer != null)
            {
                ObjectRenderers.Remove(defaultRenderer);
            }
            ObjectRenderers.AddIfNotAlready<ChtmlCustomContainerRenderer>();
        }
        
        private class ChtmlCustomContainerRenderer : HtmlObjectRenderer<CustomContainer>
        {
            protected override void Write(HtmlRenderer renderer, CustomContainer obj)
            {
                var chtmlRenderer = renderer as ChtmlMarkdownRenderer;
                if (chtmlRenderer == null)
                {
                    // Fallback - render as regular div
                    renderer.Write("<div>");
                    foreach (var child in obj)
                    {
                        renderer.Write(child);
                    }
                    renderer.Write("</div>");
                    return;
                }
                
                // Get container type (the text after :::)
                // This is the component name (e.g., "Codeblock", "Container", etc.)
                var containerType = obj.Info?.Replace("~", "").Trim();
                
                // Check if this is a component container (not a regular markdown container)
                // Component containers are those that match registered component names
                // For now, we'll treat any non-empty container type as a potential component
                // The actual component validation happens during code generation
                if (!string.IsNullOrEmpty(containerType))
                {
                    // Use container type directly as component name
                    var componentName = containerType;
                    
                    // Build attributes from container arguments
                    // Markdig stores arguments in obj.Arguments as a StringSlice
                    // For syntax like :::Codeblock {lang="csharp"}, the {lang="csharp"} part
                    // might be stored in Arguments, but we need to check if it includes the braces
                    var attributes = new StringBuilder();
                    
                    // First, try to get attributes from Generic Attributes extension
                    // When UseGenericAttributes() is enabled, we can use the extension method GetAttributes()
                    try
                    {
                        var block = obj as Block;
                        if (block != null)
                        {
                            // Try using the extension method from GenericAttributes namespace
                            // This is the proper way to access attributes when Generic Attributes extension is enabled
                            var htmlAttributes = HtmlAttributesExtensions.GetAttributes(block);
                            
                            if (htmlAttributes != null)
                            {
                                ExtractAttributesFromHtmlAttributes(htmlAttributes, attributes);
                            }
                        }
                        
                        // Check obj.Arguments as fallback (in case Generic Attributes extension isn't working)
                        if (attributes.Length == 0 && obj.Arguments != null && obj.Arguments.Length > 0)
                        {
                            var argsStr = obj.Arguments.ToString();
                            if (!string.IsNullOrWhiteSpace(argsStr))
                            {
                                var parsedAttrs = ParseAttributes(argsStr);
                                foreach (var (key, value) in parsedAttrs)
                                {
                                    attributes.Append($" {key}=\"{EscapeHtmlAttribute(value)}\"");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ignore errors when accessing attributes - fallback to obj.Arguments parsing if needed
                        System.Diagnostics.Debug.WriteLine($"Error accessing attributes via Generic Attributes extension: {ex.Message}");
                    }
                    
                    
                    
                    // Extract raw text content from container (preserve as-is for code blocks)
                    // For code blocks, we want the raw text, not HTML-rendered content
                    // Get raw source text from the original markdown using line information
                    var contentBuilder = new StringBuilder();
                    ExtractRawTextFromSource(chtmlRenderer._sourceMarkdown, obj, contentBuilder);
                    var content = contentBuilder.ToString();
                    
                    // Escape content for HTML attribute
                    var escapedContent = EscapeHtmlAttribute(content);
                    
                    // Write component tag with Content attribute
                    renderer.Write("<C:").Write(componentName);
                    if (attributes.Length > 0)
                    {
                        renderer.Write(attributes.ToString());
                    }
                    renderer.Write(" Content=\"").Write(escapedContent).Write("\"></C:").Write(componentName).Write(">");
                }
                else
                {
                    // Not a component container, render as regular HTML div using default behavior
                    renderer.Write("<div");
                    if (!string.IsNullOrEmpty(containerType))
                    {
                        renderer.Write(" class=\"").WriteEscape(containerType).Write("\"");
                    }
                    if (obj.Arguments != null && obj.Arguments.Length > 0)
                    {
                        var argsStr = obj.Arguments.ToString();
                        if (!string.IsNullOrWhiteSpace(argsStr))
                        {
                            var parsedAttrs = ParseAttributes(argsStr);
                            foreach (var (key, value) in parsedAttrs)
                            {
                                renderer.Write(" ").Write(key).Write("=\"").WriteEscape(value ?? "").Write("\"");
                            }
                        }
                    }
                    renderer.Write(">");
                    
                    // Render content
                    foreach (var child in obj)
                    {
                        renderer.Write(child);
                    }
                    
                    renderer.Write("</div>");
                }
            }
            
            private void ExtractAttributesFromHtmlAttributes(object htmlAttributes, StringBuilder attributes)
            {
                if (htmlAttributes == null)
                    return;
                
                // HtmlAttributes has Id, Classes, and Properties
                var idProperty = htmlAttributes.GetType().GetProperty("Id",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (idProperty != null)
                {
                    var id = idProperty.GetValue(htmlAttributes)?.ToString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        attributes.Append($" id=\"{EscapeHtmlAttribute(id)}\"");
                    }
                }
                
                var classesProperty = htmlAttributes.GetType().GetProperty("Classes",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                // Skip Classes - Markdig adds the container type as a class, but we don't need it for CHTML components
                // if (classesProperty != null)
                // {
                //     var classes = classesProperty.GetValue(htmlAttributes);
                //     if (classes is System.Collections.IEnumerable classList)
                //     {
                //         var classValues = new List<string>();
                //         foreach (var cls in classList)
                //         {
                //             if (cls != null)
                //                 classValues.Add(cls.ToString());
                //         }
                //         if (classValues.Count > 0)
                //         {
                //             attributes.Append($" class=\"{EscapeHtmlAttribute(string.Join(" ", classValues))}\"");
                //         }
                //     }
                // }
                
                var propertiesProperty = htmlAttributes.GetType().GetProperty("Properties",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                
                if (propertiesProperty != null)
                {
                    var properties = propertiesProperty.GetValue(htmlAttributes);
                    
                    if (properties is System.Collections.IDictionary propsDict)
                    {
                        foreach (System.Collections.DictionaryEntry entry in propsDict)
                        {
                            var key = entry.Key?.ToString();
                            var value = entry.Value?.ToString();
                            if (!string.IsNullOrEmpty(key) && value != null)
                            {
                                attributes.Append($" {key}=\"{EscapeHtmlAttribute(value)}\"");
                            }
                        }
                    }
                    else if (properties is System.Collections.IEnumerable propsList)
                    {
                        // Properties might be a List<KeyValuePair<string, string>>
                        foreach (var item in propsList)
                        {
                            if (item != null)
                            {
                                var itemType = item.GetType();
                                var keyProperty = itemType.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);
                                var valueProperty = itemType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                                
                                if (keyProperty != null && valueProperty != null)
                                {
                                    var key = keyProperty.GetValue(item)?.ToString();
                                    var value = valueProperty.GetValue(item)?.ToString();
                                    if (!string.IsNullOrEmpty(key) && value != null)
                                    {
                                        attributes.Append($" {key}=\"{EscapeHtmlAttribute(value)}\"");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            private List<(string key, string value)> ParseAttributes(string argsStr)
            {
                var attributes = new List<(string, string)>();
                if (string.IsNullOrWhiteSpace(argsStr))
                    return attributes;

                // Remove surrounding braces if present
                argsStr = argsStr.Trim();
                if (argsStr.StartsWith("{") && argsStr.EndsWith("}"))
                {
                    argsStr = argsStr.Substring(1, argsStr.Length - 2).Trim();
                }

                // Parse key="value" or key='value' pairs
                var i = 0;
                while (i < argsStr.Length)
                {
                    // Skip whitespace
                    while (i < argsStr.Length && char.IsWhiteSpace(argsStr[i]))
                        i++;

                    if (i >= argsStr.Length)
                        break;

                    // Find key
                    var keyStart = i;
                    while (i < argsStr.Length && !char.IsWhiteSpace(argsStr[i]) && argsStr[i] != '=')
                        i++;
                    var key = argsStr.Substring(keyStart, i - keyStart);

                    // Skip whitespace and =
                    while (i < argsStr.Length && (char.IsWhiteSpace(argsStr[i]) || argsStr[i] == '='))
                        i++;

                    if (i >= argsStr.Length)
                        break;

                    // Find value (quoted or unquoted)
                    string value;
                    if (argsStr[i] == '"' || argsStr[i] == '\'')
                    {
                        var quote = argsStr[i];
                        i++; // Skip opening quote
                        var valueStart = i;
                        while (i < argsStr.Length && argsStr[i] != quote)
                        {
                            if (argsStr[i] == '\\' && i + 1 < argsStr.Length)
                                i++; // Skip escaped character
                            i++;
                        }
                        value = argsStr.Substring(valueStart, i - valueStart);
                        i++; // Skip closing quote
                    }
                    else
                    {
                        // Unquoted value (until whitespace or end)
                        var valueStart = i;
                        while (i < argsStr.Length && !char.IsWhiteSpace(argsStr[i]))
                            i++;
                        value = argsStr.Substring(valueStart, i - valueStart);
                    }

                    attributes.Add((key, value));
                }

                return attributes;
            }
            
            private string EscapeHtmlAttribute(string value)
            {
                return value
                    .Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&#39;");
            }
            
            /// <summary>
            /// Extracts raw text content from the original markdown source using the container's Span property.
            /// This preserves the exact text as written, including all line breaks and formatting.
            /// Uses the Block's Span to get the full container content, then extracts content between ::: markers.
            /// </summary>
            private void ExtractRawTextFromSource(string sourceMarkdown, CustomContainer container, StringBuilder output)
            {
                // Try to use the Block's Span property to get the full container content
                try
                {
                    var block = container as Block;
                    if (block != null)
                    {
                        // Get Span property from Block base class
                        var spanProperty = typeof(Block).GetProperty("Span",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        
                        if (spanProperty != null)
                        {
                            var span = spanProperty.GetValue(block);
                            if (span != null)
                            {
                                var startProperty = span.GetType().GetProperty("Start",
                                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                                var endProperty = span.GetType().GetProperty("End",
                                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                                
                                if (startProperty != null && endProperty != null)
                                {
                                    var start = (int)(startProperty.GetValue(span) ?? 0);
                                    var end = (int)(endProperty.GetValue(span) ?? 0);
                                    
                                    if (start >= 0 && end > start && end <= sourceMarkdown.Length)
                                    {
                                        // Extract the full container text including markers
                                        // Use the original source markdown to preserve all whitespace exactly
                                        var containerText = sourceMarkdown.Substring(start, end - start);
                                        
                                        // Split by newlines preserving the original line structure
                                        // We need to handle both \r\n and \n
                                        var lines = new List<string>();
                                        var currentLine = new StringBuilder();
                                        for (int i = 0; i < containerText.Length; i++)
                                        {
                                            var ch = containerText[i];
                                            if (ch == '\r')
                                            {
                                                // Check if next is \n
                                                if (i + 1 < containerText.Length && containerText[i + 1] == '\n')
                                                {
                                                    lines.Add(currentLine.ToString());
                                                    currentLine.Clear();
                                                    i++; // Skip \n
                                                }
                                                else
                                                {
                                                    lines.Add(currentLine.ToString());
                                                    currentLine.Clear();
                                                }
                                            }
                                            else if (ch == '\n')
                                            {
                                                lines.Add(currentLine.ToString());
                                                currentLine.Clear();
                                            }
                                            else
                                            {
                                                currentLine.Append(ch);
                                            }
                                        }
                                        if (currentLine.Length > 0)
                                        {
                                            lines.Add(currentLine.ToString());
                                        }
                                        
                                        var contentLines = new List<string>();
                                        bool inContent = false;
                                        
                                        foreach (var line in lines)
                                        {
                                            var trimmed = line.TrimStart();
                                            if (trimmed.StartsWith(":::"))
                                            {
                                                if (!inContent)
                                                {
                                                    // Found opening marker - start collecting content on next line
                                                    inContent = true;
                                                }
                                                else
                                                {
                                                    // Found closing marker - stop collecting
                                                    break;
                                                }
                                            }
                                            else if (inContent)
                                            {
                                                // Collect content lines preserving ALL whitespace (including leading spaces)
                                                contentLines.Add(line);
                                            }
                                        }
                                        
                                        if (contentLines.Count > 0)
                                        {
                                            // Join lines preserving newlines and all whitespace
                                            output.Append(string.Join("\n", contentLines));
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Fall through to Lines extraction
                    System.Diagnostics.Debug.WriteLine($"Span extraction failed: {ex.Message}");
                }
                
                // Fallback: Try to access Lines property directly
                try
                {
                    // Access the Lines property (protected, so use reflection)
                    var linesProperty = typeof(ContainerBlock).GetProperty("Lines",
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    
                    if (linesProperty != null)
                    {
                        var linesObj = linesProperty.GetValue(container);
                        if (linesObj != null)
                        {
                            // StringLineGroup has a Lines property that returns StringLine[]
                            var linesArrayProperty = linesObj.GetType().GetProperty("Lines",
                                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                            
                            if (linesArrayProperty != null)
                            {
                                var linesArray = linesArrayProperty.GetValue(linesObj);
                                if (linesArray is System.Collections.IEnumerable enumerable)
                                {
                                    var contentLines = new List<string>();
                                    bool foundContent = false;
                                    
                                    foreach (var lineObj in enumerable)
                                    {
                                        if (lineObj == null) continue;
                                        
                                        // StringLine has a Slice property of type StringSlice
                                        var sliceProperty = lineObj.GetType().GetProperty("Slice",
                                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                                        
                                        if (sliceProperty != null)
                                        {
                                            var slice = sliceProperty.GetValue(lineObj);
                                            if (slice != null)
                                            {
                                                // StringSlice has a Text property that points to the original source
                                                // and Start/End properties for the position
                                                var textProperty = slice.GetType().GetProperty("Text",
                                                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                                                var startProperty = slice.GetType().GetProperty("Start",
                                                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                                                var endProperty = slice.GetType().GetProperty("End",
                                                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                                                
                                                if (textProperty != null && startProperty != null && endProperty != null)
                                                {
                                                    var text = textProperty.GetValue(slice);
                                                    var start = (int)(startProperty.GetValue(slice) ?? 0);
                                                    var end = (int)(endProperty.GetValue(slice) ?? 0);
                                                    
                                                    // If Text points to the source markdown, use it directly
                                                    if (text != null && ReferenceEquals(text, sourceMarkdown))
                                                    {
                                                        if (start >= 0 && end > start && end <= sourceMarkdown.Length)
                                                        {
                                                            // Extract directly from source to preserve all whitespace
                                                            var lineText = sourceMarkdown.Substring(start, end - start);
                                                            if (!string.IsNullOrEmpty(lineText))
                                                            {
                                                                var trimmed = lineText.TrimStart();
                                                                // Skip container markers (:::)
                                                                if (trimmed.StartsWith(":::"))
                                                                {
                                                                    continue;
                                                                }
                                                                
                                                                foundContent = true;
                                                                // Preserve the original line including ALL leading whitespace
                                                                contentLines.Add(lineText);
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Text might be a different string - use ToString() as fallback
                                                        var lineText = slice.ToString();
                                                        if (!string.IsNullOrEmpty(lineText))
                                                        {
                                                            var trimmed = lineText.TrimStart();
                                                            // Skip container markers (:::)
                                                            if (trimmed.StartsWith(":::"))
                                                            {
                                                                continue;
                                                            }
                                                            
                                                            foundContent = true;
                                                            // Preserve the original line including leading whitespace
                                                            contentLines.Add(lineText);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    // Fallback to ToString() if we can't get positions
                                                    var lineText = slice.ToString();
                                                    if (!string.IsNullOrEmpty(lineText))
                                                    {
                                                        var trimmed = lineText.TrimStart();
                                                        // Skip container markers (:::)
                                                        if (trimmed.StartsWith(":::"))
                                                        {
                                                            continue;
                                                        }
                                                        
                                                        foundContent = true;
                                                        // Preserve the original line including leading whitespace
                                                        contentLines.Add(lineText);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    
                                    if (foundContent)
                                    {
                                        // Join lines preserving newlines
                                        output.Append(string.Join("\n", contentLines));
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Fall through to block extraction
                    System.Diagnostics.Debug.WriteLine($"Lines extraction failed: {ex.Message}");
                }
                
                // Fallback: extract from child blocks - ensure we get ALL content
                // This is critical - we need to process ALL child blocks, not just the first one
                ExtractRawTextContent(container, output, sourceMarkdown);
            }
            
            /// <summary>
            /// Extracts raw text content from markdown blocks, preserving the original text.
            /// </summary>
            private void ExtractRawTextContent(ContainerBlock container, StringBuilder output, string sourceMarkdown)
            {
                var isFirstBlock = true;
                foreach (var child in container)
                {
                    // Add newline between blocks (except before first block)
                    if (!isFirstBlock)
                    {
                        output.AppendLine();
                    }
                    isFirstBlock = false;
                    
                    // Handle fenced code blocks (like ```csharp)
                    if (child is FencedCodeBlock fencedCodeBlock)
                    {
                        if (fencedCodeBlock.Lines.Count > 0)
                        {
                            foreach (var line in fencedCodeBlock.Lines.Lines)
                            {
                                if (line.Slice.Text != null)
                                {
                                    output.AppendLine(line.Slice.ToString());
                                }
                            }
                        }
                        continue;
                    }
                    
                    if (child is LeafBlock leafBlock)
                    {
                        // Try to get raw lines first - this preserves exact formatting including line breaks
                        // ParagraphBlocks and other LeafBlocks should have Lines property
                        if (leafBlock.Lines.Count > 0)
                        {
                            foreach (var line in leafBlock.Lines.Lines)
                            {
                                if (line.Slice.Text != null)
                                {
                                    var lineText = line.Slice.ToString();
                                    output.AppendLine(lineText);
                                }
                            }
                        }
                        // Fallback: if no lines but has inline content, extract inline text
                        // For ParagraphBlocks, we need to get ALL inline content, not just the first
                        else if (leafBlock.Inline != null)
                        {
                            ExtractInlineText(leafBlock.Inline, output);
                            // Don't add newline here - it will be added between blocks
                        }
                        // If neither Lines nor Inline, try to get content from Block's Span if available
                        else
                        {
                            // Try to extract using Block's Span property via reflection
                            try
                            {
                                var block = leafBlock as Block;
                                if (block != null)
                                {
                                    var spanProperty = typeof(Block).GetProperty("Span", BindingFlags.Public | BindingFlags.Instance);
                                    if (spanProperty != null)
                                    {
                                        var span = spanProperty.GetValue(block);
                                        if (span != null)
                                        {
                                            var startProp = span.GetType().GetProperty("Start");
                                            var endProp = span.GetType().GetProperty("End");
                                            if (startProp != null && endProp != null)
                                            {
                                                var start = (int)(startProp.GetValue(span) ?? 0);
                                                var end = (int)(endProp.GetValue(span) ?? 0);
                                                if (start >= 0 && end > start && end <= sourceMarkdown.Length)
                                                {
                                                    var content = sourceMarkdown.Substring(start, end - start);
                                                    output.Append(content);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore - fall through
                            }
                        }
                    }
                    else if (child is ContainerBlock containerBlock)
                    {
                        ExtractRawTextContent(containerBlock, output, sourceMarkdown);
                    }
                }
            }
            
            /// <summary>
            /// Extracts text from inline elements, preserving all content including line breaks.
            /// </summary>
            private void ExtractInlineText(Inline inline, StringBuilder output)
            {
                var current = inline;
                while (current != null)
                {
                    if (current is LiteralInline literal)
                    {
                        output.Append(literal.Content.ToString());
                    }
                    else if (current is LineBreakInline)
                    {
                        output.AppendLine();
                    }
                    else if (current is ContainerInline container && container.FirstChild != null)
                    {
                        ExtractInlineText(container.FirstChild, output);
                    }
                    // Handle other inline types that might contain text
                    else if (current is Markdig.Syntax.Inlines.CodeInline codeInline)
                    {
                        output.Append(codeInline.Content.ToString());
                    }
                    current = current.NextSibling;
                }
            }
        }
    }
}

