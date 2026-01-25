using System.Text;
using Femur.Parsing.Nodes;
using Femur.Markdown.Abstractions;
using Femur.Markdown.Abstractions.Nodes;
#if NETSTANDARD2_0
using Femur.Markdown.Renderer.Extensions;
#endif


namespace Femur.Markdown.Renderer;

/// <summary>
/// HTML renderer for Markdown AST.
/// Converts a Markdown AST to HTML by walking the tree and generating HTML output.
/// </summary>
public class MarkdownHtmlRenderer : MarkdownAstWalker
{
    private readonly StringBuilder _output;

    /// <summary>
    /// Creates a new HTML renderer.
    /// </summary>
    public MarkdownHtmlRenderer()
    {
        this._output = new StringBuilder();
    }

    /// <summary>
    /// Renders the Markdown document to HTML.
    /// </summary>
    public string Render(MarkdownDocumentNode document)
    {
        _ = this._output.Clear();
        this.Walk(document);
        return this._output.ToString();
    }

    /// <summary>
    /// Renders the Markdown document to HTML and writes to a stream.
    /// </summary>
    public void RenderToStream(MarkdownDocumentNode document, Stream stream, Encoding? encoding = null)
    {
        _ = this._output.Clear();
        this.Walk(document);

        using var writer = new StreamWriter(stream, encoding ?? Encoding.UTF8, bufferSize: 8192, leaveOpen: true);
        writer.Write(this._output.ToString());
        writer.Flush();
    }

    /// <summary>
    /// Escapes HTML special characters in text content.
    /// Must replace &amp; first to avoid double-escaping.
    /// Also escapes curly quotes that may have been introduced by smart punctuation.
    /// </summary>
    private static string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("\u201C", "&quot;")  // Left double quotation mark
            .Replace("\u201D", "&quot;")  // Right double quotation mark
            .Replace("'", "&#39;")
            .Replace("\u2018", "&#39;")    // Left single quotation mark
            .Replace("\u2019", "&#39;");   // Right single quotation mark
    }

    /// <summary>
    /// Escapes HTML attributes (URLs, titles, etc.).
    /// Must replace &amp; first to avoid double-escaping.
    /// Also escapes curly quotes that may have been introduced by smart punctuation.
    /// </summary>
    private static string EscapeHtmlAttribute(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("\u201C", "&quot;")  // Left double quotation mark
            .Replace("\u201D", "&quot;");  // Right double quotation mark
    }

    /// <summary>
    /// Renders inline children of a container node.
    /// </summary>
    private void RenderInlineChildren(MarkdownContainerNode container)
    {
        this.WalkChildren(container);
    }

    protected override void VisitHeading(HeadingNode node)
    {
        _ = this._output.Append($"<h{node.Level}>");
        this.RenderInlineChildren(node);
        _ = this._output.Append($"</h{node.Level}>");
    }

    protected override void VisitParagraph(ParagraphNode node)
    {
        _ = this._output.Append("<p>");
        this.RenderInlineChildren(node);
        _ = this._output.Append("</p>");
    }

    protected override void VisitBlockQuote(BlockQuoteNode node)
    {
        _ = this._output.Append("<blockquote>");
        this.WalkChildren(node);
        _ = this._output.Append("</blockquote>");
    }

    protected override void VisitCodeBlock(CodeBlockNode node)
    {
        if (node.IsFenced && !string.IsNullOrEmpty(node.Info))
        {
            var language = EscapeHtmlAttribute(node.Info!.Trim());
            _ = this._output.Append($"<pre><code class=\"language-{language}\">");
        }
        else
        {
            _ = this._output.Append("<pre><code>");
        }

        // Preserve trailing newline for fenced code blocks (CommonMark spec)
        var originalContent = node.Content;
        var content = EscapeHtml(originalContent);
        if (node.IsFenced && !string.IsNullOrEmpty(originalContent) && !originalContent.EndsWith('\n'))
        {
            content += '\n';
        }

        _ = this._output.Append(content);
        _ = this._output.Append("</code></pre>");
    }

    protected override void VisitList(ListNode node)
    {
        if (node.IsOrdered)
        {
            if (node.StartNumber != 1)
            {
                _ = this._output.Append($"<ol start=\"{node.StartNumber}\">");
            }
            else
            {
                _ = this._output.Append("<ol>");
            }
        }
        else
        {
            _ = this._output.Append("<ul>");
        }

        this.WalkChildren(node);
        _ = this._output.Append(node.IsOrdered ? "</ol>" : "</ul>");
    }

    protected override void VisitListItem(ListItemNode node)
    {
        _ = this._output.Append("<li>");

        // If list item contains only a single paragraph, render its inline children directly without <p> tags
        if (node.Children.Count == 1 && node.Children[0] is ParagraphNode paragraph)
        {
            // Render inline children directly, skipping the paragraph wrapper
            this.RenderInlineChildren(paragraph);
        }
        else
        {
            // Walk all children, but skip paragraph nodes and render their inline children instead
            foreach (var child in node.Children)
            {
                if (child is ParagraphNode para)
                {
                    // Render paragraph's inline children without <p> wrapper
                    this.RenderInlineChildren(para);
                }
                else
                {
                    // Render other block elements normally
                    this.VisitNode(child);
                }
            }
        }

        _ = this._output.Append("</li>");
    }

    protected override void VisitThematicBreak(ThematicBreakNode node)
    {
        _ = this._output.Append("<hr />");
    }

    protected override void VisitHtmlBlock(HtmlBlockNode node)
    {
        // Output raw HTML content (already escaped by parser if needed)
        _ = this._output.Append(node.Content);
    }

    protected override void VisitFencedDiv(FencedDivNode node)
    {
        // Determine tag name: use Tag if present, otherwise default to "div"
        var tagName = !string.IsNullOrEmpty(node.Tag) ? node.Tag : "div";

        // Build attributes
        var attributes = new StringBuilder();
        var hasAnyAttributes = false;

        // Check if there are parsed attributes
        var hasParsedAttributes = node.ParsedAttributes.Classes.Count > 0 ||
                                  !string.IsNullOrEmpty(node.ParsedAttributes.Id) ||
                                  node.ParsedAttributes.KeyValueAttributes.Count > 0;

        // Add ID if present
        if (!string.IsNullOrEmpty(node.ParsedAttributes.Id))
        {
            attributes.Append($" id=\"{EscapeHtmlAttribute(node.ParsedAttributes.Id!)}\"");
            hasAnyAttributes = true;
        }

        // Add classes if present
        if (node.ParsedAttributes.Classes.Count > 0)
        {
            var classList = string.Join(" ", node.ParsedAttributes.Classes.Select(c => EscapeHtmlAttribute(c)));
            attributes.Append($" class=\"{classList}\"");
            hasAnyAttributes = true;
        }

        // Add key-value attributes
        foreach (var kvp in node.ParsedAttributes.KeyValueAttributes)
        {
            attributes.Append($" {EscapeHtmlAttribute(kvp.Key)}=\"{EscapeHtmlAttribute(kvp.Value)}\"");
            hasAnyAttributes = true;
        }

        // If tag exists but no other attributes, add tag as class
        // This handles cases like :::warning → <div class="warning"> or :::a → <a class="a">
        if (!string.IsNullOrEmpty(node.Tag) && !hasParsedAttributes && string.IsNullOrEmpty(node.Attributes))
        {
            // Special case: if tag is a single word that looks like a CSS class (not an HTML tag),
            // use "div" as tag and tag as class. Otherwise use tag as HTML tag and add as class.
            // For now, we'll use tag as HTML tag and add as class for consistency
            if (!hasAnyAttributes)
            {
                attributes.Append($" class=\"{EscapeHtmlAttribute(node.Tag!)}\"");
                hasAnyAttributes = true;
            }
        }

        // Render opening tag
        _ = this._output.Append($"<{tagName}");
        if (hasAnyAttributes)
        {
            _ = this._output.Append(attributes);
        }

        _ = this._output.Append('>');

        // Walk children if present, otherwise render rawContent
        if (node.HasChildren)
        {
            this.WalkChildren(node);
        }
        else if (!string.IsNullOrEmpty(node.RawContent))
        {
            // For tags with colon convention (e.g., C:Codeblock) that skip parsing,
            // render the raw content as escaped text
            _ = this._output.Append(EscapeHtml(node.RawContent));
        }

        // Render closing tag
        _ = this._output.Append($"</{tagName}>");
    }

    protected override void VisitEmphasis(EmphasisNode node)
    {
        _ = this._output.Append("<em>");
        this.RenderInlineChildren(node);
        _ = this._output.Append("</em>");
    }

    protected override void VisitStrongEmphasis(StrongEmphasisNode node)
    {
        _ = this._output.Append("<strong>");
        this.RenderInlineChildren(node);
        _ = this._output.Append("</strong>");
    }

    protected override void VisitLink(LinkNode node)
    {
        var url = EscapeHtmlAttribute(node.Url);
        _ = this._output.Append($"<a href=\"{url}\"");

        if (!string.IsNullOrEmpty(node.Title))
        {
            var title = EscapeHtmlAttribute(node.Title!);
            _ = this._output.Append($" title=\"{title}\"");
        }

        _ = this._output.Append('>');
        this.RenderInlineChildren(node);
        _ = this._output.Append("</a>");
    }

    protected override void VisitImage(ImageNode node)
    {
        var url = EscapeHtmlAttribute(node.Url);
        _ = this._output.Append($"<img src=\"{url}\"");

        // Render alt text from children
        var altText = new StringBuilder();
        this.RenderInlineChildren(node, altText);
        var alt = EscapeHtmlAttribute(altText.ToString());
        _ = this._output.Append($" alt=\"{alt}\"");

        if (!string.IsNullOrEmpty(node.Title))
        {
            var title = EscapeHtmlAttribute(node.Title!);
            _ = this._output.Append($" title=\"{title}\"");
        }

        _ = this._output.Append(" />");
    }

    protected override void VisitCodeSpan(CodeSpanNode node)
    {
        _ = this._output.Append("<code>");
        _ = this._output.Append(EscapeHtml(node.Content));
        _ = this._output.Append("</code>");
    }

    protected override void VisitHardLineBreak(HardLineBreakNode node)
    {
        _ = this._output.Append("<br />");
    }

    protected override void VisitSoftLineBreak(SoftLineBreakNode node)
    {
        _ = this._output.Append('\n');
    }

    protected override void VisitText(MarkdownTextNode node)
    {
        _ = this._output.Append(EscapeHtml(node.Content));
    }

    /// <summary>
    /// Helper method to render inline children to a specific StringBuilder.
    /// Used for extracting alt text from image nodes.
    /// </summary>
    private void RenderInlineChildren(MarkdownContainerNode container, StringBuilder output)
    {
        foreach (var child in container.Children)
        {
            this.RenderNodeToBuilder(child, output);
        }
    }

    /// <summary>
    /// Renders a single node to a StringBuilder.
    /// Used for extracting text content (e.g., alt text from images).
    /// </summary>
    private void RenderNodeToBuilder(Node node, StringBuilder output)
    {
        if (node == null)
        {
            return;
        }

        var nodeType = node.NodeType;
        if (nodeType == MarkdownNodeType.Emphasis)
        {
            this.RenderNodeToBuilder((EmphasisNode)node, output);
        }
        else if (nodeType == MarkdownNodeType.StrongEmphasis)
        {
            this.RenderNodeToBuilder((StrongEmphasisNode)node, output);
        }
        else if (nodeType == MarkdownNodeType.Link)
        {
            this.RenderNodeToBuilder((LinkNode)node, output);
        }
        else if (nodeType == MarkdownNodeType.CodeSpan)
        {
            this.RenderNodeToBuilder((CodeSpanNode)node, output);
        }
        else if (nodeType == MarkdownNodeType.HardLineBreak)
        {
            _ = output.Append(' ');
        }
        else if (nodeType == MarkdownNodeType.SoftLineBreak)
        {
            _ = output.Append(' ');
        }
        else if (nodeType == NodeType.Text)
        {
            _ = output.Append(((MarkdownTextNode)node).Content);
        }
        else
        {
            // For other node types, try to extract text from children
            if (node is MarkdownContainerNode container)
            {
                this.RenderInlineChildren(container, output);
            }
        }
    }

    private void RenderNodeToBuilder(EmphasisNode node, StringBuilder output)
    {
        this.RenderInlineChildren(node, output);
    }

    private void RenderNodeToBuilder(StrongEmphasisNode node, StringBuilder output)
    {
        this.RenderInlineChildren(node, output);
    }

    private void RenderNodeToBuilder(LinkNode node, StringBuilder output)
    {
        this.RenderInlineChildren(node, output);
    }

    private void RenderNodeToBuilder(CodeSpanNode node, StringBuilder output)
    {
        _ = output.Append(node.Content);
    }
}