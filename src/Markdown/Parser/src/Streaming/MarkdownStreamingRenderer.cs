namespace Femur.Markdown.Parser.Streaming;

/// <summary>
/// Abstract base class for streaming Markdown renderers.
/// Provides virtual methods that are called during parsing to render output incrementally
/// without building an intermediate node tree.
/// </summary>
public abstract class MarkdownStreamingRenderer : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Called at the start of document parsing.
    /// </summary>
    public virtual void OnDocumentStart()
    {
    }

    /// <summary>
    /// Called at the end of document parsing.
    /// </summary>
    public virtual void OnDocumentEnd()
    {
    }

    // Block-level elements

    /// <summary>
    /// Called when entering a heading block.
    /// </summary>
    /// <param name="level">Heading level (1-6)</param>
    public virtual void OnEnterHeading(int level)
    {
    }

    /// <summary>
    /// Called when exiting a heading block.
    /// </summary>
    /// <param name="level">Heading level (1-6)</param>
    public virtual void OnExitHeading(int level)
    {
    }

    /// <summary>
    /// Called when entering a paragraph.
    /// </summary>
    public virtual void OnEnterParagraph()
    {
    }

    /// <summary>
    /// Called when exiting a paragraph.
    /// </summary>
    public virtual void OnExitParagraph()
    {
    }

    /// <summary>
    /// Called when entering a blockquote.
    /// </summary>
    public virtual void OnEnterBlockQuote()
    {
    }

    /// <summary>
    /// Called when exiting a blockquote.
    /// </summary>
    public virtual void OnExitBlockQuote()
    {
    }

    /// <summary>
    /// Called when entering a list.
    /// </summary>
    /// <param name="isOrdered">True if ordered list, false if unordered</param>
    /// <param name="startNumber">Starting number for ordered lists</param>
    public virtual void OnEnterList(bool isOrdered, int startNumber = 1)
    {
    }

    /// <summary>
    /// Called when exiting a list.
    /// </summary>
    /// <param name="isOrdered">True if ordered list, false if unordered</param>
    public virtual void OnExitList(bool isOrdered)
    {
    }

    /// <summary>
    /// Called when entering a list item.
    /// </summary>
    public virtual void OnEnterListItem()
    {
    }

    /// <summary>
    /// Called when exiting a list item.
    /// </summary>
    public virtual void OnExitListItem()
    {
    }

    /// <summary>
    /// Called when rendering a code block.
    /// </summary>
    /// <param name="code">The code content</param>
    /// <param name="language">Optional language identifier</param>
    public virtual void OnCodeBlock(string code, string? language = null)
    {
    }

    /// <summary>
    /// Called when rendering an HTML block.
    /// </summary>
    /// <param name="html">The raw HTML content</param>
    public virtual void OnHtmlBlock(string html)
    {
    }

    /// <summary>
    /// Called when rendering a thematic break (horizontal rule).
    /// </summary>
    public virtual void OnThematicBreak()
    {
    }

    // Inline-level elements

    /// <summary>
    /// Called when rendering text content.
    /// </summary>
    /// <param name="text">The text content</param>
    public virtual void OnText(string text)
    {
    }

    /// <summary>
    /// Called when entering an emphasis span.
    /// </summary>
    public virtual void OnEnterEmphasis()
    {
    }

    /// <summary>
    /// Called when exiting an emphasis span.
    /// </summary>
    public virtual void OnExitEmphasis()
    {
    }

    /// <summary>
    /// Called when entering a strong emphasis span.
    /// </summary>
    public virtual void OnEnterStrongEmphasis()
    {
    }

    /// <summary>
    /// Called when exiting a strong emphasis span.
    /// </summary>
    public virtual void OnExitStrongEmphasis()
    {
    }

    /// <summary>
    /// Called when rendering a code span.
    /// </summary>
    /// <param name="code">The code content</param>
    public virtual void OnCodeSpan(string code)
    {
    }

    /// <summary>
    /// Called when entering a link.
    /// </summary>
    /// <param name="url">The link URL</param>
    /// <param name="title">Optional link title</param>
    public virtual void OnEnterLink(string url, string? title = null)
    {
    }

    /// <summary>
    /// Called when exiting a link.
    /// </summary>
    public virtual void OnExitLink()
    {
    }

    /// <summary>
    /// Called when rendering an image.
    /// </summary>
    /// <param name="url">The image URL</param>
    /// <param name="altText">The alt text</param>
    /// <param name="title">Optional image title</param>
    public virtual void OnImage(string url, string altText, string? title = null)
    {
    }

    /// <summary>
    /// Called when rendering a hard line break.
    /// </summary>
    public virtual void OnHardLineBreak()
    {
    }

    /// <summary>
    /// Called when rendering a soft line break.
    /// </summary>
    public virtual void OnSoftLineBreak()
    {
    }

    /// <summary>
    /// Disposes of resources used by the renderer.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of resources used by the renderer.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._disposed)
        {
            if (disposing)
            {
                // Derived classes can override to dispose their resources
            }

            this._disposed = true;
        }
    }
}