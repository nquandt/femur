using System.Text;
using Femur.Markdown.Parser.Compatibility;

namespace Femur.Markdown.Parser.Streaming;

/// <summary>
/// Streaming Markdown parser that invokes renderer callbacks during parsing
/// instead of building an intermediate node tree.
/// Implements a subset of CommonMark 0.31.2 specification optimized for streaming.
/// </summary>
public sealed class MarkdownStreamingParser : IDisposable
{
    private readonly Stream _stream;
    private readonly MarkdownStreamingRenderer _renderer;
    private readonly int _bufferSize;
    private StreamReader? _reader;
    private bool _disposed;

    private List<string>? _lines;
    private StringBuilder? _currentLine;
    private Dictionary<string, LinkReference>? _linkReferences;

    /// <summary>
    /// Link reference definition for inline parsing
    /// </summary>
    private sealed class LinkReference
    {
        public string Url { get; set; } = string.Empty;
        public string? Title { get; set; }
    }

    /// <summary>
    /// Creates a new streaming Markdown parser.
    /// </summary>
    /// <param name="stream">The stream to read Markdown from</param>
    /// <param name="renderer">The renderer to invoke during parsing</param>
    /// <param name="bufferSize">Size of the read buffer (default 4096)</param>
    public MarkdownStreamingParser(Stream stream, MarkdownStreamingRenderer renderer, int bufferSize = 4096)
    {
        this._stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this._renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        this._bufferSize = bufferSize;
    }

    /// <summary>
    /// Parses the Markdown stream and invokes renderer callbacks.
    /// </summary>
    public void Parse()
    {
        this._reader = new StreamReader(this._stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: this._bufferSize, leaveOpen: false);
        this._lines = new List<string>();
        this._currentLine = new StringBuilder();
        this._linkReferences = new Dictionary<string, LinkReference>(StringComparer.OrdinalIgnoreCase);

        // Read all lines
        this.ReadLines();

        // Notify start of document
        this._renderer.OnDocumentStart();

        // Parse and render
        this.ParseBlocks();

        // Notify end of document
        this._renderer.OnDocumentEnd();
    }

    private void ReadLines()
    {
        string? line;
        while ((line = this._reader!.ReadLine()) != null)
        {
            this._lines!.Add(line);
        }
    }

    #region Block Parsing

    private void ParseBlocks()
    {
        var i = 0;
        while (i < this._lines!.Count)
        {
            var line = this._lines[i];

            // Skip blank lines
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;

            // Try different block types in order
            if (this.TryParseAtxHeading(line, ref i))
            {
                continue;
            }

            if (this.TryParseThematicBreak(line, ref i))
            {
                continue;
            }

            if (this.TryParseFencedCodeBlock(line, ref i))
            {
                continue;
            }

            if (indent >= 4 && this.TryParseIndentedCodeBlock(line, ref i))
            {
                continue;
            }

            if (this.TryParseBlockQuote(line, ref i))
            {
                continue;
            }

            if (this.TryParseList(line, ref i))
            {
                continue;
            }

            if (this.TryParseLinkReferenceDefinition(line, ref i))
            {
                continue;
            }

            // Default: paragraph
            this.ParseParagraph(line, ref i);
        }
    }

    private bool TryParseAtxHeading(string line, ref int index)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '#')
        {
            return false;
        }

        // Count heading level
        var level = 0;
        while (level < trimmed.Length && level < 6 && trimmed[level] == '#')
        {
            level++;
        }

        if (level == 0)
        {
            return false;
        }

        // Must be followed by space or end of line
        if (level < trimmed.Length && trimmed[level] != ' ' && trimmed[level] != '\t')
        {
            return false;
        }

        // Extract heading text using span operations
        var textSpan = level < trimmed.Length ? trimmed.AsSpan(level).TrimStart() : ReadOnlySpan<char>.Empty;

        // Remove trailing # (do both trim operations in one go with span)
        textSpan = textSpan.TrimEnd('#').TrimEnd();

        var text = textSpan.ToString();

        this._renderer.OnEnterHeading(level);
        this.ParseInline(text);
        this._renderer.OnExitHeading(level);

        index++;
        return true;
    }

    private bool TryParseThematicBreak(string line, ref int index)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3)
        {
            return false;
        }

        var ch = trimmed[0];
        if (ch != '*' && ch != '-' && ch != '_')
        {
            return false;
        }

        // Count markers and check for invalid characters
        var count = 0;
        foreach (var c in trimmed)
        {
            if (c == ch)
            {
                count++;
            }
            else if (c != ' ' && c != '\t')
            {
                return false;
            }
        }

        if (count < 3)
        {
            return false;
        }

        this._renderer.OnThematicBreak();
        index++;
        return true;
    }

    private bool TryParseFencedCodeBlock(string line, ref int index)
    {
        var trimmed = line.TrimStart();
        var indent = line.Length - trimmed.Length;

        if (indent >= 4 || trimmed.Length < 3)
        {
            return false;
        }

        var fence = trimmed[0];
        if (fence != '`' && fence != '~')
        {
            return false;
        }

        // Count fence length
        var fenceLen = 0;
        while (fenceLen < trimmed.Length && trimmed[fenceLen] == fence)
        {
            fenceLen++;
        }

        if (fenceLen < 3)
        {
            return false;
        }

        // Extract info string (language) using span to avoid allocation
        var info = trimmed.AsSpan(fenceLen).Trim().ToString();

        // Collect code lines
        var code = new StringBuilder();
        index++;

        while (index < this._lines!.Count)
        {
            var codeLine = this._lines[index];
            var codeTrimmed = codeLine.TrimStart();

            // Check for closing fence
            var closeCount = 0;
            while (closeCount < codeTrimmed.Length && codeTrimmed[closeCount] == fence)
            {
                closeCount++;
            }

            if (closeCount >= fenceLen && codeTrimmed.AsSpan(closeCount).Trim().Length == 0)
            {
                index++;
                break;
            }

            if (code.Length > 0)
            {
                _ = code.Append('\n');
            }

            _ = code.Append(codeLine);
            index++;
        }

        this._renderer.OnCodeBlock(code.ToString(), string.IsNullOrEmpty(info) ? null : info);
        return true;
    }

    private bool TryParseIndentedCodeBlock(string line, ref int index)
    {
        var code = new StringBuilder();

        while (index < this._lines!.Count)
        {
            var codeLine = this._lines[index];

            // Blank lines are included
            if (string.IsNullOrWhiteSpace(codeLine))
            {
                _ = code.Append('\n');
                index++;
                continue;
            }

            // Must have 4+ space indent
            var trimmed = codeLine.TrimStart();
            var indent = codeLine.Length - trimmed.Length;

            if (indent < 4)
            {
                break;
            }

            if (code.Length > 0)
            {
                _ = code.Append('\n');
            }

            // Remove 4 spaces of indent
            _ = code.Append(codeLine.Length >= 4 ? codeLine.AsSpan(4) : codeLine.AsSpan());
            index++;
        }

        if (code.Length > 0)
        {
            this._renderer.OnCodeBlock(code.ToString().TrimEnd('\n'));
            return true;
        }

        return false;
    }

    private bool TryParseBlockQuote(string line, ref int index)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '>')
        {
            return false;
        }

        this._renderer.OnEnterBlockQuote();

        // Collect and parse blockquote content
        var quoteLines = new List<string>();
        while (index < this._lines!.Count)
        {
            var quoteLine = this._lines[index];
            var quoteTrimmed = quoteLine.TrimStart();

            if (quoteTrimmed.Length == 0 || quoteTrimmed[0] != '>')
            {
                break;
            }

            // Remove > and optional space using spans
            var contentSpan = quoteTrimmed.AsSpan(1);
            if (contentSpan.Length > 0 && (contentSpan[0] == ' ' || contentSpan[0] == '\t'))
            {
                contentSpan = contentSpan[1..];
            }

            var content = contentSpan.ToString();

            quoteLines.Add(content);
            index++;
        }

        // Recursively parse quote content
        var savedLines = this._lines;
        this._lines = quoteLines;
        var quoteIndex = 0;

        while (quoteIndex < this._lines.Count)
        {
            var quoteLine = this._lines[quoteIndex];

            if (string.IsNullOrWhiteSpace(quoteLine))
            {
                quoteIndex++;
                continue;
            }

            if (!this.TryParseAtxHeading(quoteLine, ref quoteIndex) &&
                !this.TryParseThematicBreak(quoteLine, ref quoteIndex) &&
                !this.TryParseFencedCodeBlock(quoteLine, ref quoteIndex) &&
                !this.TryParseList(quoteLine, ref quoteIndex))
            {
                this.ParseParagraph(quoteLine, ref quoteIndex);
            }
        }

        this._lines = savedLines;
        this._renderer.OnExitBlockQuote();

        return true;
    }

    private bool TryParseList(string line, ref int index)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        // Check for ordered list
        var isOrdered = false;
        var startNum = 1;
        var markerLen = 0;

        if (char.IsDigit(trimmed[0]))
        {
            var digits = 0;
            while (digits < trimmed.Length && char.IsDigit(trimmed[digits]))
            {
                digits++;
            }

            if (digits > 0 && digits < trimmed.Length &&
                (trimmed[digits] == '.' || trimmed[digits] == ')') &&
                digits + 1 < trimmed.Length &&
                (trimmed[digits + 1] == ' ' || trimmed[digits + 1] == '\t'))
            {
                isOrdered = true;
                startNum = Int32Compat.Parse(trimmed.AsSpan(0, digits));
                markerLen = digits + 1;
            }
        }

        // Check for unordered list
        if (!isOrdered)
        {
            if (trimmed.Length > 1 &&
                (trimmed[0] == '-' || trimmed[0] == '+' || trimmed[0] == '*') &&
                (trimmed[1] == ' ' || trimmed[1] == '\t'))
            {
                markerLen = 1;
            }
            else
            {
                return false;
            }
        }

        this._renderer.OnEnterList(isOrdered, startNum);

        // Parse list items
        while (index < this._lines!.Count)
        {
            var itemLine = this._lines[index];
            var itemTrimmed = itemLine.TrimStart();

            if (string.IsNullOrWhiteSpace(itemLine))
            {
                index++;
                continue;
            }

            // Check if this line starts a list item
            var isItem = false;

            if (isOrdered && itemTrimmed.Length > 0 && char.IsDigit(itemTrimmed[0]))
            {
                var digits = 0;
                while (digits < itemTrimmed.Length && char.IsDigit(itemTrimmed[digits]))
                {
                    digits++;
                }

                if (digits > 0 && digits < itemTrimmed.Length &&
                    (itemTrimmed[digits] == '.' || itemTrimmed[digits] == ')') &&
                    digits + 1 < itemTrimmed.Length &&
                    (itemTrimmed[digits + 1] == ' ' || itemTrimmed[digits + 1] == '\t'))
                {
                    isItem = true;
                    markerLen = digits + 1;
                }
            }
            else if (!isOrdered && itemTrimmed.Length > 1 &&
                     (itemTrimmed[0] == '-' || itemTrimmed[0] == '+' || itemTrimmed[0] == '*') &&
                     (itemTrimmed[1] == ' ' || itemTrimmed[1] == '\t'))
            {
                isItem = true;
                markerLen = 1;
            }

            if (!isItem)
            {
                break;
            }

            // Extract item content using span to trim without intermediate allocation
            var content = itemTrimmed.Length > markerLen
                ? itemTrimmed.AsSpan(markerLen).TrimStart().ToString()
                : string.Empty;

            this._renderer.OnEnterListItem();
            this.ParseInline(content);
            this._renderer.OnExitListItem();

            index++;
        }

        this._renderer.OnExitList(isOrdered);
        return true;
    }

    private bool TryParseLinkReferenceDefinition(string line, ref int index)
    {
        // Simplified link reference parsing - just skip for now
        // Full implementation would extract [label]: url "title"
        return false;
    }

    private void ParseParagraph(string line, ref int index)
    {
        this._renderer.OnEnterParagraph();

        // Collect paragraph lines
        var para = new StringBuilder();
        _ = para.Append(line);

        index++;
        while (index < this._lines!.Count)
        {
            var nextLine = this._lines[index];

            // Break on blank line
            if (string.IsNullOrWhiteSpace(nextLine))
            {
                break;
            }

            var trimmed = nextLine.TrimStart();

            // Break on other block starts
            if (trimmed.Length > 0 &&
                (trimmed[0] == '#' || trimmed[0] == '>' ||
                 this.IsThematicBreak(trimmed) ||
                 trimmed.StartsWith("```") || trimmed.StartsWith("~~~")))
            {
                break;
            }

            _ = para.Append('\n');
            _ = para.Append(nextLine);
            index++;
        }

        this.ParseInline(para.ToString());
        this._renderer.OnExitParagraph();
    }

    private bool IsThematicBreak(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3)
        {
            return false;
        }

        var ch = trimmed[0];
        if (ch != '*' && ch != '-' && ch != '_')
        {
            return false;
        }

        var count = 0;
        foreach (var c in trimmed)
        {
            if (c == ch)
            {
                count++;
            }
            else if (c != ' ' && c != '\t')
            {
                return false;
            }
        }

        return count >= 3;
    }

    #endregion

    #region Inline Parsing

    private void ParseInline(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var i = 0;
        while (i < text.Length)
        {
            var ch = text[i];

            // Code spans
            if (ch == '`')
            {
                if (this.TryParseCodeSpan(text, ref i))
                {
                    continue;
                }
            }

            // Images (must check before links since it starts with !)
            if (ch == '!' && i + 1 < text.Length && text[i + 1] == '[')
            {
                if (this.TryParseImage(text, ref i))
                {
                    continue;
                }
            }

            // Links
            if (ch == '[')
            {
                if (this.TryParseLink(text, ref i))
                {
                    continue;
                }
            }

            // Emphasis
            if (ch == '*' || ch == '_')
            {
                if (this.TryParseEmphasis(text, ref i))
                {
                    continue;
                }
            }

            // Hard line breaks
            if (ch == '\\' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                this._renderer.OnHardLineBreak();
                i += 2;
                continue;
            }

            // Soft line breaks
            if (ch == '\n')
            {
                this._renderer.OnSoftLineBreak();
                i++;
                continue;
            }

            // Regular text
            var start = i;
            while (i < text.Length)
            {
                ch = text[i];
                if (ch == '`' || ch == '[' || ch == '!' || ch == '*' || ch == '_' || ch == '\\' || ch == '\n')
                {
                    break;
                }

                i++;
            }

            if (i > start)
            {
                this._renderer.OnText(text.AsSpan(start, i - start).ToString());
            }
            else if (i < text.Length)
            {
                // No special syntax matched and no regular text consumed.
                // Treat the current character as literal text and advance.
                this._renderer.OnText(text.AsSpan(i, 1).ToString());
                i++;
            }
        }
    }

    private bool TryParseCodeSpan(string text, ref int i)
    {
        var start = i;
        var backticks = 0;

        // Count opening backticks
        while (i < text.Length && text[i] == '`')
        {
            backticks++;
            i++;
        }

        var contentStart = i;

        // Find closing backticks
        while (i < text.Length)
        {
            if (text[i] == '`')
            {
                var closeCount = 0;
                var closeStart = i;

                while (i < text.Length && text[i] == '`')
                {
                    closeCount++;
                    i++;
                }

                if (closeCount == backticks)
                {
                    // Use span for trimming to avoid intermediate allocation
                    var codeSpan = text.AsSpan(contentStart, closeStart - contentStart).Trim();
                    this._renderer.OnCodeSpan(codeSpan.ToString());
                    return true;
                }
            }
            else
            {
                i++;
            }
        }

        // No match
        i = start + 1;
        return false;
    }

    private bool TryParseLink(string text, ref int i)
    {
        var start = i;
        i++; // Skip [

        // Find ]
        var textEnd = text.IndexOf(']', i);
        if (textEnd == -1)
        {
            i = start + 1;
            return false;
        }

        var linkText = text.AsSpan(i, textEnd - i).ToString();
        i = textEnd + 1;

        // Check for (url)
        if (i < text.Length && text[i] == '(')
        {
            i++; // Skip (
            var urlEnd = text.IndexOf(')', i);
            if (urlEnd == -1)
            {
                i = start + 1;
                return false;
            }

            var url = text.AsSpan(i, urlEnd - i).Trim().ToString();
            i = urlEnd + 1;

            this._renderer.OnEnterLink(url);
            this.ParseInline(linkText);
            this._renderer.OnExitLink();
            return true;
        }

        i = start + 1;
        return false;
    }

    private bool TryParseImage(string text, ref int i)
    {
        // i points to !, next char should be [
        var start = i;
        i++; // Skip !

        if (i >= text.Length || text[i] != '[')
        {
            i = start + 1;
            return false;
        }

        i++; // Skip [

        // Find ]
        var textEnd = text.IndexOf(']', i);
        if (textEnd == -1)
        {
            i = start + 1;
            return false;
        }

        var altText = text.AsSpan(i, textEnd - i).ToString();
        i = textEnd + 1;

        // Check for (url)
        if (i < text.Length && text[i] == '(')
        {
            i++; // Skip (
            var urlEnd = text.IndexOf(')', i);
            if (urlEnd == -1)
            {
                i = start + 1;
                return false;
            }

            var url = text.AsSpan(i, urlEnd - i).Trim().ToString();
            i = urlEnd + 1;

            this._renderer.OnImage(url, altText);
            return true;
        }

        i = start + 1;
        return false;
    }

    private bool TryParseEmphasis(string text, ref int i)
    {
        var marker = text[i];
        var start = i;

        // Count markers
        var count = 0;
        while (i < text.Length && text[i] == marker)
        {
            count++;
            i++;
        }

        // Try strong emphasis first (** or __)
        if (count >= 2)
        {
            var closeMarker = new string(marker, 2);
            var closeIndex = text.IndexOf(closeMarker, i);
            if (closeIndex != -1)
            {
                var emphText = text.AsSpan(i, closeIndex - i).ToString();
                this._renderer.OnEnterStrongEmphasis();
                this.ParseInline(emphText);
                this._renderer.OnExitStrongEmphasis();
                i = closeIndex + 2;
                return true;
            }
        }

        // Try regular emphasis (* or _)
        if (count >= 1)
        {
            var closeIndex = text.IndexOf(marker, i);
            if (closeIndex != -1)
            {
                var emphText = text.AsSpan(i, closeIndex - i).ToString();
                this._renderer.OnEnterEmphasis();
                this.ParseInline(emphText);
                this._renderer.OnExitEmphasis();
                i = closeIndex + 1;
                return true;
            }
        }

        // No match
        i = start + 1;
        return false;
    }

    #endregion

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!this._disposed)
        {
            if (disposing)
            {
                this._reader?.Dispose();
            }

            this._disposed = true;
        }
    }
}