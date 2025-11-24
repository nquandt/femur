namespace Femur.Markdown.Parser.Streaming;

/// <summary>
/// Interface for streaming Markdown renderers that accept ReadOnlySpan&lt;char&gt; parameters
/// for zero-allocation rendering. Implement this with a ref struct for maximum performance.
/// </summary>
public interface IMarkdownStreamingRenderer
{
    /// <summary>
    /// Called at the start of document parsing.
    /// </summary>
    void OnDocumentStart();

    /// <summary>
    /// Called at the end of document parsing.
    /// </summary>
    void OnDocumentEnd();

    // Block-level elements

    /// <summary>
    /// Called when entering a heading block.
    /// </summary>
    /// <param name="level">Heading level (1-6)</param>
    void OnEnterHeading(int level);

    /// <summary>
    /// Called when exiting a heading block.
    /// </summary>
    /// <param name="level">Heading level (1-6)</param>
    void OnExitHeading(int level);

    /// <summary>
    /// Called when entering a paragraph.
    /// </summary>
    void OnEnterParagraph();

    /// <summary>
    /// Called when exiting a paragraph.
    /// </summary>
    void OnExitParagraph();

    /// <summary>
    /// Called when entering a blockquote.
    /// </summary>
    void OnEnterBlockQuote();

    /// <summary>
    /// Called when exiting a blockquote.
    /// </summary>
    void OnExitBlockQuote();

    /// <summary>
    /// Called when entering a list.
    /// </summary>
    /// <param name="isOrdered">True if ordered list, false if unordered</param>
    /// <param name="startNumber">Starting number for ordered lists</param>
    void OnEnterList(bool isOrdered, int startNumber = 1);

    /// <summary>
    /// Called when exiting a list.
    /// </summary>
    /// <param name="isOrdered">True if ordered list, false if unordered</param>
    void OnExitList(bool isOrdered);

    /// <summary>
    /// Called when entering a list item.
    /// </summary>
    void OnEnterListItem();

    /// <summary>
    /// Called when exiting a list item.
    /// </summary>
    void OnExitListItem();

    /// <summary>
    /// Called when rendering a code block.
    /// </summary>
    /// <param name="code">The code content</param>
    /// <param name="language">Optional language identifier</param>
    void OnCodeBlock(ReadOnlySpan<char> code, ReadOnlySpan<char> language);

    /// <summary>
    /// Called when rendering an HTML block.
    /// </summary>
    /// <param name="html">The raw HTML content</param>
    void OnHtmlBlock(ReadOnlySpan<char> html);

    /// <summary>
    /// Called when rendering a thematic break (horizontal rule).
    /// </summary>
    void OnThematicBreak();

    // Inline-level elements

    /// <summary>
    /// Called when rendering text content.
    /// </summary>
    /// <param name="text">The text content</param>
    void OnText(ReadOnlySpan<char> text);

    /// <summary>
    /// Called when entering an emphasis span.
    /// </summary>
    void OnEnterEmphasis();

    /// <summary>
    /// Called when exiting an emphasis span.
    /// </summary>
    void OnExitEmphasis();

    /// <summary>
    /// Called when entering a strong emphasis span.
    /// </summary>
    void OnEnterStrongEmphasis();

    /// <summary>
    /// Called when exiting a strong emphasis span.
    /// </summary>
    void OnExitStrongEmphasis();

    /// <summary>
    /// Called when rendering a code span.
    /// </summary>
    /// <param name="code">The code content</param>
    void OnCodeSpan(ReadOnlySpan<char> code);

    /// <summary>
    /// Called when entering a link.
    /// </summary>
    /// <param name="url">The link URL</param>
    /// <param name="title">Optional link title</param>
    void OnEnterLink(ReadOnlySpan<char> url, ReadOnlySpan<char> title);

    /// <summary>
    /// Called when exiting a link.
    /// </summary>
    void OnExitLink();

    /// <summary>
    /// Called when rendering an image.
    /// </summary>
    /// <param name="url">The image URL</param>
    /// <param name="altText">The alt text</param>
    /// <param name="title">Optional image title</param>
    void OnImage(ReadOnlySpan<char> url, ReadOnlySpan<char> altText, ReadOnlySpan<char> title);

    /// <summary>
    /// Called when rendering a hard line break.
    /// </summary>
    void OnHardLineBreak();

    /// <summary>
    /// Called when rendering a soft line break.
    /// </summary>
    void OnSoftLineBreak();
}
