namespace Femur.Markdown.Parser.Streaming;

/// <summary>
/// Zero-allocation streaming Markdown to HTML renderer using ReadOnlySpan&lt;char&gt; parameters.
/// Writes HTML output directly to a StreamWriter as Markdown is parsed,
/// accepting ReadOnlySpan&lt;char&gt; parameters to avoid string allocations.
/// </summary>
public sealed class SpanMarkdownHtmlRenderer : IMarkdownStreamingRenderer, IDisposable
{
    private readonly StreamWriter _writer;

    /// <summary>
    /// Creates a new span-based HTML streaming renderer that writes to the specified StreamWriter.
    /// </summary>
    /// <param name="writer">The StreamWriter to write HTML output to</param>
    public SpanMarkdownHtmlRenderer(StreamWriter writer)
    {
        this._writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    // Block-level elements

    public void OnDocumentStart()
    {
        // Optional: could write <!DOCTYPE html> if rendering full document
    }

    public void OnDocumentEnd()
    {
        // Flush at end
        this._writer.Flush();
    }

    public void OnEnterHeading(int level)
    {
        this._writer.Write("<h");
        this._writer.Write(level);
        this._writer.Write('>');
    }

    public void OnExitHeading(int level)
    {
        this._writer.Write("</h");
        this._writer.Write(level);
        this._writer.WriteLine('>');
    }

    public void OnEnterParagraph()
    {
        this._writer.Write("<p>");
    }

    public void OnExitParagraph()
    {
        this._writer.WriteLine("</p>");
    }

    public void OnEnterBlockQuote()
    {
        this._writer.WriteLine("<blockquote>");
    }

    public void OnExitBlockQuote()
    {
        this._writer.WriteLine("</blockquote>");
    }

    public void OnEnterList(bool isOrdered, int startNumber = 1)
    {
        if (isOrdered)
        {
            if (startNumber != 1)
            {
                this._writer.Write("<ol start=\"");
                this._writer.Write(startNumber);
                this._writer.WriteLine("\">");
            }
            else
            {
                this._writer.WriteLine("<ol>");
            }
        }
        else
        {
            this._writer.WriteLine("<ul>");
        }
    }

    public void OnExitList(bool isOrdered)
    {
        this._writer.WriteLine(isOrdered ? "</ol>" : "</ul>");
    }

    public void OnEnterListItem()
    {
        this._writer.Write("<li>");
    }

    public void OnExitListItem()
    {
        this._writer.WriteLine("</li>");
    }

    public void OnCodeBlock(ReadOnlySpan<char> code, ReadOnlySpan<char> language)
    {
        this._writer.Write("<pre><code");
        if (language.Length > 0)
        {
            this._writer.Write(" class=\"language-");
            this.WriteHtmlEscaped(language);
            this._writer.Write('"');
        }

        this._writer.Write('>');
        this.WriteHtmlEscaped(code);
        this._writer.WriteLine("</code></pre>");
    }

    public void OnHtmlBlock(ReadOnlySpan<char> html)
    {
        // Write raw HTML - spans allow us to write directly
        this._writer.WriteLine(html);
    }

    public void OnThematicBreak()
    {
        this._writer.WriteLine("<hr />");
    }

    // Inline-level elements

    public void OnText(ReadOnlySpan<char> text)
    {
        this.WriteHtmlEscaped(text);
    }

    public void OnEnterEmphasis()
    {
        this._writer.Write("<em>");
    }

    public void OnExitEmphasis()
    {
        this._writer.Write("</em>");
    }

    public void OnEnterStrongEmphasis()
    {
        this._writer.Write("<strong>");
    }

    public void OnExitStrongEmphasis()
    {
        this._writer.Write("</strong>");
    }

    public void OnCodeSpan(ReadOnlySpan<char> code)
    {
        this._writer.Write("<code>");
        this.WriteHtmlEscaped(code);
        this._writer.Write("</code>");
    }

    public void OnEnterLink(ReadOnlySpan<char> url, ReadOnlySpan<char> title)
    {
        this._writer.Write("<a href=\"");
        this.WriteAttributeEscaped(url);
        this._writer.Write('"');

        if (title.Length > 0)
        {
            this._writer.Write(" title=\"");
            this.WriteAttributeEscaped(title);
            this._writer.Write('"');
        }

        this._writer.Write('>');
    }

    public void OnExitLink()
    {
        this._writer.Write("</a>");
    }

    public void OnImage(ReadOnlySpan<char> url, ReadOnlySpan<char> altText, ReadOnlySpan<char> title)
    {
        this._writer.Write("<img src=\"");
        this.WriteAttributeEscaped(url);
        this._writer.Write("\" alt=\"");
        this.WriteAttributeEscaped(altText);
        this._writer.Write('"');

        if (title.Length > 0)
        {
            this._writer.Write(" title=\"");
            this.WriteAttributeEscaped(title);
            this._writer.Write('"');
        }

        this._writer.Write(" />");
    }

    public void OnHardLineBreak()
    {
        this._writer.Write("<br />");
    }

    public void OnSoftLineBreak()
    {
        this._writer.Write('\n');
    }

    // HTML Escaping with spans - zero allocation

    private void WriteHtmlEscaped(ReadOnlySpan<char> text)
    {
        var lastWritten = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var escape = text[i] switch
            {
                '<' => "&lt;",
                '>' => "&gt;",
                '&' => "&amp;",
                '"' => "&quot;",
                _ => null
            };

            if (escape != null)
            {
                // Write unescaped portion
                if (i > lastWritten)
                {
                    this._writer.Write(text.Slice(lastWritten, i - lastWritten));
                }

                // Write escape sequence
                this._writer.Write(escape);
                lastWritten = i + 1;
            }
        }

        // Write remaining unescaped portion
        if (lastWritten < text.Length)
        {
            this._writer.Write(text[lastWritten..]);
        }
    }

    private void WriteAttributeEscaped(ReadOnlySpan<char> text)
    {
        var lastWritten = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var escape = text[i] switch
            {
                '<' => "&lt;",
                '>' => "&gt;",
                '&' => "&amp;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => null
            };

            if (escape != null)
            {
                // Write unescaped portion
                if (i > lastWritten)
                {
                    this._writer.Write(text.Slice(lastWritten, i - lastWritten));
                }

                // Write escape sequence
                this._writer.Write(escape);
                lastWritten = i + 1;
            }
        }

        // Write remaining unescaped portion
        if (lastWritten < text.Length)
        {
            this._writer.Write(text[lastWritten..]);
        }
    }

    /// <summary>
    /// Disposes of resources. Since we don't own the writer, this is a no-op.
    /// </summary>
    public void Dispose()
    {
        // We don't own the writer, so nothing to dispose
    }
}
