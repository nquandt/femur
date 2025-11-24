using System.Text;

namespace Femur.Markdown.Parser.Streaming;

/// <summary>
/// Streaming Markdown to HTML renderer.
/// Writes HTML output directly to a StreamWriter as Markdown is parsed,
/// without building an intermediate node tree.
/// </summary>
public class MarkdownHtmlStreamingRenderer : MarkdownStreamingRenderer
{
    private readonly StreamWriter _writer;
    private readonly bool _ownsWriter;

    /// <summary>
    /// Creates a new HTML streaming renderer that writes to the specified StreamWriter.
    /// </summary>
    /// <param name="writer">The StreamWriter to write HTML output to</param>
    /// <param name="ownsWriter">If true, the writer will be disposed when this renderer is disposed</param>
    public MarkdownHtmlStreamingRenderer(StreamWriter writer, bool ownsWriter = false)
    {
        this._writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this._ownsWriter = ownsWriter;
    }

    /// <summary>
    /// Creates a new HTML streaming renderer that writes to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to write HTML output to</param>
    /// <param name="encoding">The text encoding to use (defaults to UTF-8)</param>
    public MarkdownHtmlStreamingRenderer(Stream stream, Encoding? encoding = null)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        this._writer = new StreamWriter(stream, encoding ?? Encoding.UTF8);
        this._ownsWriter = true;
    }

    // Block-level elements

    public override void OnEnterHeading(int level)
    {
        this._writer.Write($"<h{level}>");
    }

    public override void OnExitHeading(int level)
    {
        this._writer.WriteLine($"</h{level}>");
    }

    public override void OnEnterParagraph()
    {
        this._writer.Write("<p>");
    }

    public override void OnExitParagraph()
    {
        this._writer.WriteLine("</p>");
    }

    public override void OnEnterBlockQuote()
    {
        this._writer.WriteLine("<blockquote>");
    }

    public override void OnExitBlockQuote()
    {
        this._writer.WriteLine("</blockquote>");
    }

    public override void OnEnterList(bool isOrdered, int startNumber = 1)
    {
        if (isOrdered)
        {
            if (startNumber != 1)
            {
                this._writer.WriteLine($"<ol start=\"{startNumber}\">");
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

    public override void OnExitList(bool isOrdered)
    {
        this._writer.WriteLine(isOrdered ? "</ol>" : "</ul>");
    }

    public override void OnEnterListItem()
    {
        this._writer.Write("<li>");
    }

    public override void OnExitListItem()
    {
        this._writer.WriteLine("</li>");
    }

    public override void OnCodeBlock(string code, string? language = null)
    {
        this._writer.Write("<pre><code");
        if (!string.IsNullOrEmpty(language))
        {
            this._writer.Write(" class=\"language-");
            this.WriteHtmlEscaped(language!);
            this._writer.Write('"');
        }

        this._writer.Write(">");
        this.WriteHtmlEscaped(code);
        this._writer.WriteLine("</code></pre>");
    }

    public override void OnHtmlBlock(string html)
    {
        this._writer.WriteLine(html);
    }

    public override void OnThematicBreak()
    {
        this._writer.WriteLine("<hr />");
    }

    // Inline-level elements

    public override void OnText(string text)
    {
        this.WriteHtmlEscaped(text);
    }

    public override void OnEnterEmphasis()
    {
        this._writer.Write("<em>");
    }

    public override void OnExitEmphasis()
    {
        this._writer.Write("</em>");
    }

    public override void OnEnterStrongEmphasis()
    {
        this._writer.Write("<strong>");
    }

    public override void OnExitStrongEmphasis()
    {
        this._writer.Write("</strong>");
    }

    public override void OnCodeSpan(string code)
    {
        this._writer.Write("<code>");
        this.WriteHtmlEscaped(code);
        this._writer.Write("</code>");
    }

    public override void OnEnterLink(string url, string? title = null)
    {
        this._writer.Write("<a href=\"");
        this.WriteAttributeEscaped(url);
        this._writer.Write('"');
        if (!string.IsNullOrEmpty(title))
        {
            this._writer.Write(" title=\"");
            this.WriteAttributeEscaped(title!);
            this._writer.Write('"');
        }

        this._writer.Write(">");
    }

    public override void OnExitLink()
    {
        this._writer.Write("</a>");
    }

    public override void OnImage(string url, string altText, string? title = null)
    {
        this._writer.Write("<img src=\"");
        this.WriteAttributeEscaped(url);
        this._writer.Write("\" alt=\"");
        this.WriteAttributeEscaped(altText);
        this._writer.Write('"');
        if (!string.IsNullOrEmpty(title))
        {
            this._writer.Write(" title=\"");
            this.WriteAttributeEscaped(title!);
            this._writer.Write('"');
        }

        this._writer.Write(" />");
    }

    public override void OnHardLineBreak()
    {
        this._writer.WriteLine("<br />");
    }

    public override void OnSoftLineBreak()
    {
        this._writer.WriteLine();
    }

    /// <summary>
    /// Fast HTML escape for text content - only escapes &lt;, &gt;, &amp;, and &quot;
    /// Much faster than HttpUtility.HtmlEncode for our use case.
    /// </summary>
    private void WriteHtmlEscaped(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var lastPos = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var escaped = text[i] switch
            {
                '<' => "&lt;",
                '>' => "&gt;",
                '&' => "&amp;",
                '"' => "&quot;",
                _ => null
            };

            if (escaped != null)
            {
                // Write any pending unescaped text
                if (i > lastPos)
                {
                    this._writer.Write(text.AsSpan(lastPos, i - lastPos));
                }

                this._writer.Write(escaped);
                lastPos = i + 1;
            }
        }

        // Write remaining unescaped text
        if (lastPos < text.Length)
        {
            this._writer.Write(text.AsSpan(lastPos));
        }
    }

    /// <summary>
    /// Fast HTML attribute escape - escapes &lt;, &gt;, &amp;, &quot;, and single quote
    /// </summary>
    private void WriteAttributeEscaped(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var lastPos = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var escaped = text[i] switch
            {
                '<' => "&lt;",
                '>' => "&gt;",
                '&' => "&amp;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => null
            };

            if (escaped != null)
            {
                // Write any pending unescaped text
                if (i > lastPos)
                {
                    this._writer.Write(text.AsSpan(lastPos, i - lastPos));
                }

                this._writer.Write(escaped);
                lastPos = i + 1;
            }
        }

        // Write remaining unescaped text
        if (lastPos < text.Length)
        {
            this._writer.Write(text.AsSpan(lastPos));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && this._ownsWriter)
        {
            this._writer?.Dispose();
        }

        base.Dispose(disposing);
    }
}