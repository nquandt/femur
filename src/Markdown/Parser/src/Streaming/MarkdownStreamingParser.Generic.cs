using System.Text;
using Femur.Markdown.Parser.Compatibility;

namespace Femur.Markdown.Parser.Streaming;

/// <summary>
/// Generic streaming Markdown parser that invokes renderer callbacks during parsing
/// instead of building an intermediate node tree. Accepts any renderer implementing IMarkdownStreamingRenderer.
/// Implements a subset of CommonMark 0.31.2 specification optimized for streaming with zero-allocation spans.
/// </summary>
/// <typeparam name="TRenderer">The renderer type implementing IMarkdownStreamingRenderer</typeparam>
public sealed class MarkdownStreamingParser<TRenderer> : IDisposable
    where TRenderer : IMarkdownStreamingRenderer
{
    private readonly Stream _stream;
    private readonly TRenderer _renderer;
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
    public MarkdownStreamingParser(Stream stream, TRenderer renderer, int bufferSize = 4096)
    {
        this._stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this._renderer = renderer;
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

            if (this.TryParseFencedCodeBlock(line, ref i))
            {
                continue;
            }

            if (indent >= 4 && this.TryParseIndentedCodeBlock(line, ref i))
            {
                continue;
            }

            if (this.TryParseThematicBreak(trimmed))
            {
                i++;
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

            // Otherwise, treat as paragraph
            this.ParseParagraph(ref i);
        }
    }

    private bool TryParseAtxHeading(string line, ref int i)
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

        // Must have space or end of line after #'s
        if (level < trimmed.Length && trimmed[level] != ' ' && trimmed[level] != '\t')
        {
            return false;
        }

        // Extract heading text without ToString() - use span operations
        var textSpan = trimmed.AsSpan(level).TrimStart();
        textSpan = textSpan.TrimEnd('#').TrimEnd();

        this._renderer.OnEnterHeading(level);
        this.ParseInline(textSpan);
        this._renderer.OnExitHeading(level);

        i++;
        return true;
    }

    private bool TryParseFencedCodeBlock(string line, ref int i)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length < 3)
        {
            return false;
        }

        var fenceChar = trimmed[0];
        if (fenceChar != '`' && fenceChar != '~')
        {
            return false;
        }

        // Count fence length
        var fenceLen = 0;
        while (fenceLen < trimmed.Length && trimmed[fenceLen] == fenceChar)
        {
            fenceLen++;
        }

        if (fenceLen < 3)
        {
            return false;
        }

        // Get info string (language) - use span without allocation
        var infoSpan = trimmed.AsSpan(fenceLen).Trim();

        var code = new StringBuilder();
        i++; // Move past opening fence

        // Collect code lines until closing fence
        while (i < this._lines!.Count)
        {
            var codeLine = this._lines[i];
            var codeTrimmed = codeLine.TrimStart();

            // Check for closing fence
            var closeCount = 0;
            while (closeCount < codeTrimmed.Length && codeTrimmed[closeCount] == fenceChar)
            {
                closeCount++;
            }

            if (closeCount >= fenceLen && codeTrimmed.AsSpan(closeCount).Trim().Length == 0)
            {
                // Found closing fence
                i++;
                break;
            }

            // Not a closing fence, append line
            if (code.Length > 0)
            {
                _ = code.AppendLine();
            }

            _ = code.Append(codeLine);
            i++;
        }

        // Render code block with span
        this._renderer.OnCodeBlock(code.ToString().AsSpan(), infoSpan);
        return true;
    }

    private bool TryParseIndentedCodeBlock(string line, ref int i)
    {
        var code = new StringBuilder();

        while (i < this._lines!.Count)
        {
            var codeLine = this._lines[i];

            // Blank lines are part of code block
            if (string.IsNullOrWhiteSpace(codeLine))
            {
                _ = code.AppendLine();
                i++;
                continue;
            }

            // Must be indented by at least 4 spaces
            var indent = 0;
            var charCount = 0;
            while (charCount < codeLine.Length && indent < 4 && (codeLine[charCount] == ' ' || codeLine[charCount] == '\t'))
            {
                if (codeLine[charCount] == '\t')
                {
                    indent += 4; // Tab counts as 4 spaces
                }
                else
                {
                    indent++;
                }

                charCount++;
            }

            if (indent < 4)
            {
                break;
            }

            // Append code without leading indentation - use span to avoid substring
            if (code.Length > 0)
            {
                _ = code.AppendLine();
            }

            // Skip the indentation characters we counted
            _ = code.Append(codeLine.AsSpan(charCount));
            i++;
        }

        if (code.Length > 0)
        {
            // Render with span, empty language span
            this._renderer.OnCodeBlock(code.ToString().AsSpan().TrimEnd(), ReadOnlySpan<char>.Empty);
            return true;
        }

        return false;
    }

    private bool TryParseThematicBreak(string trimmed)
    {
        if (trimmed.Length < 3)
        {
            return false;
        }

        var c = trimmed[0];
        if (c != '-' && c != '_' && c != '*')
        {
            return false;
        }

        var count = 0;
        foreach (var ch in trimmed)
        {
            if (ch == c)
            {
                count++;
            }
            else if (ch != ' ' && ch != '\t')
            {
                return false;
            }
        }

        if (count >= 3)
        {
            this._renderer.OnThematicBreak();
            return true;
        }

        return false;
    }

    private bool TryParseBlockQuote(string line, ref int i)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '>')
        {
            return false;
        }

        this._renderer.OnEnterBlockQuote();

        while (i < this._lines!.Count)
        {
            var quoteLine = this._lines[i];
            var quoteTrimmed = quoteLine.TrimStart();

            if (quoteTrimmed.Length == 0 || quoteTrimmed[0] != '>')
            {
                break;
            }

            // Extract content after '>' with span
            var contentSpan = quoteTrimmed.AsSpan(1);
            if (contentSpan.Length > 0 && contentSpan[0] == ' ')
            {
                contentSpan = contentSpan[1..];
            }

            // Recursively parse the quoted content
            if (contentSpan.TrimStart().Length > 0)
            {
                this._renderer.OnEnterParagraph();
                this.ParseInline(contentSpan);
                this._renderer.OnExitParagraph();
            }

            i++;
        }

        this._renderer.OnExitBlockQuote();
        return true;
    }

    private bool TryParseList(string line, ref int i)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var isOrdered = false;
        var startNum = 1;
        var markerLen = 0;

        // Check for ordered list
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
        while (i < this._lines!.Count)
        {
            var itemLine = this._lines[i];
            var itemTrimmed = itemLine.TrimStart();

            if (string.IsNullOrWhiteSpace(itemLine))
            {
                i++;
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
            var itemContentSpan = itemTrimmed.Length > markerLen
                ? itemTrimmed.AsSpan(markerLen).TrimStart()
                : ReadOnlySpan<char>.Empty;

            this._renderer.OnEnterListItem();
            this.ParseInline(itemContentSpan);
            this._renderer.OnExitListItem();

            i++;
        }

        this._renderer.OnExitList(isOrdered);
        return true;
    }

    private void ParseParagraph(ref int i)
    {
        var para = new StringBuilder();
        var startIndex = i;

        while (i < this._lines!.Count)
        {
            var line = this._lines[i];

            // Stop at blank line
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            // Stop at block-level element
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#') || trimmed.StartsWith('>') || trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                break;
            }

            if (para.Length > 0)
            {
                _ = para.Append(' ');
            }

            _ = para.Append(trimmed);
            i++;
        }

        // Safety: ensure we always consume at least one line to avoid infinite loops
        if (i == startIndex)
        {
            i++;
        }

        if (para.Length > 0)
        {
            this._renderer.OnEnterParagraph();
            this.ParseInline(para.ToString().AsSpan());
            this._renderer.OnExitParagraph();
        }
    }

    #endregion

    #region Inline Parsing

    private void ParseInline(ReadOnlySpan<char> text)
    {
        var i = 0;
        var start = 0;

        while (i < text.Length)
        {
            // Safety: ensure i is within bounds (defensive check for bugs in Try* methods)
            if (i >= text.Length)
            {
                break;
            }

            var c = text[i];

            // Code span (backtick)
            if (c == '`')
            {
                if (this.TryParseCodeSpan(text, ref i, ref start))
                {
                    // Safety: clamp i to valid range in case of bugs in parsing methods
                    if (i > text.Length)
                    {
                        i = text.Length;
                    }

                    continue;
                }

                // Safety: also clamp if parsing failed
                if (i > text.Length)
                {
                    i = text.Length;
                }
            }

            // Link or image
            if (c == '[')
            {
                if (this.TryParseLink(text, ref i, ref start))
                {
                    if (i > text.Length)
                    {
                        i = text.Length;
                    }

                    continue;
                }

                if (i > text.Length)
                {
                    i = text.Length;
                }
            }

            if (c == '!' && i + 1 < text.Length && text[i + 1] == '[')
            {
                if (this.TryParseImage(text, ref i, ref start))
                {
                    if (i > text.Length)
                    {
                        i = text.Length;
                    }

                    continue;
                }

                if (i > text.Length)
                {
                    i = text.Length;
                }
            }

            // Emphasis or strong emphasis
            if (c == '*' || c == '_')
            {
                if (this.TryParseEmphasis(text, ref i, ref start))
                {
                    if (i > text.Length)
                    {
                        i = text.Length;
                    }

                    continue;
                }

                if (i > text.Length)
                {
                    i = text.Length;
                }
            }

            // Line breaks
            if (c == '\\' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                if (i > start)
                {
                    this._renderer.OnText(text[start..i]);
                }

                this._renderer.OnHardLineBreak();
                i += 2;
                start = i;

                if (i > text.Length)
                {
                    i = text.Length;
                }

                continue;
            }

            i++;

            // Safety: clamp i after increment
            if (i > text.Length)
            {
                i = text.Length;
            }
        }

        // Emit remaining text
        if (i > start)
        {
            // Safety: ensure bounds are valid
            if (start < 0 || i < 0 || start > text.Length || i > text.Length)
            {
                throw new InvalidOperationException($"Invalid ParseInline state: start={start}, i={i}, text.Length={text.Length}");
            }

            this._renderer.OnText(text[start..i]);
        }
    }

    private bool TryParseCodeSpan(ReadOnlySpan<char> text, ref int i, ref int start)
    {
        var backtickStart = i;
        var backtickCount = 0;

        // Count opening backticks
        while (i < text.Length && text[i] == '`')
        {
            backtickCount++;
            i++;
        }

        if (backtickCount == 0)
        {
            return false;
        }

        var contentStart = i;

        // Find matching closing backticks
        while (i < text.Length)
        {
            if (text[i] != '`')
            {
                i++;
                continue;
            }

            var closeStart = i;
            var closeCount = 0;

            while (i < text.Length && text[i] == '`')
            {
                closeCount++;
                i++;
            }

            if (closeCount == backtickCount)
            {
                // Found matching close - emit text before code span
                if (backtickStart > start)
                {
                    this._renderer.OnText(text[start..backtickStart]);
                }

                // Render code span with trim on span
                var codeSpan = text.Slice(contentStart, closeStart - contentStart).Trim();
                this._renderer.OnCodeSpan(codeSpan);

                start = i;
                return true;
            }
        }

        // No matching close found
        i = backtickStart + 1;
        return false;
    }

    private bool TryParseLink(ReadOnlySpan<char> text, ref int i, ref int start)
    {
        var linkStart = i;
        i++; // Skip '['

        // Find closing ]
        var depth = 1;
        var linkTextStart = i;

        while (i < text.Length && depth > 0)
        {
            if (text[i] == '[')
            {
                depth++;
            }
            else if (text[i] == ']')
            {
                depth--;
            }

            i++;
        }

        if (depth != 0 || i >= text.Length || text[i] != '(')
        {
            i = linkStart + 1;
            return false;
        }

        var linkTextEnd = i - 1;
        i++; // Skip '('

        // Find closing )
        var urlStart = i;
        while (i < text.Length && text[i] != ')')
        {
            i++;
        }

        if (i >= text.Length)
        {
            i = linkStart + 1;
            return false;
        }

        var urlEnd = i;
        i++; // Skip ')'

        // Emit text before link
        if (linkStart > start)
        {
            this._renderer.OnText(text[start..linkStart]);
        }

        // Extract and trim with spans
        var linkTextSpan = text.Slice(linkTextStart, linkTextEnd - linkTextStart).Trim();
        var urlSpan = text.Slice(urlStart, urlEnd - urlStart).Trim();

        this._renderer.OnEnterLink(urlSpan, ReadOnlySpan<char>.Empty);
        if (linkTextSpan.Length > 0)
        {
            this.ParseInline(linkTextSpan);
        }

        this._renderer.OnExitLink();

        start = i;
        return true;
    }

    private bool TryParseImage(ReadOnlySpan<char> text, ref int i, ref int start)
    {
        var imageStart = i;
        i += 2; // Skip '!['

        // Find closing ]
        var altTextStart = i;
        while (i < text.Length && text[i] != ']')
        {
            i++;
        }

        if (i >= text.Length || text[i] != ']')
        {
            i = imageStart + 1;
            return false;
        }

        var altTextEnd = i;
        i++; // Skip ']'

        if (i >= text.Length || text[i] != '(')
        {
            i = imageStart + 1;
            return false;
        }

        i++; // Skip '('

        // Find closing )
        var urlStart = i;
        while (i < text.Length && text[i] != ')')
        {
            i++;
        }

        if (i >= text.Length)
        {
            i = imageStart + 1;
            return false;
        }

        var urlEnd = i;
        i++; // Skip ')'

        // Emit text before image
        if (imageStart > start)
        {
            this._renderer.OnText(text[start..imageStart]);
        }

        // Extract and trim with spans
        var altTextSpan = text.Slice(altTextStart, altTextEnd - altTextStart).Trim();
        var urlSpan = text.Slice(urlStart, urlEnd - urlStart).Trim();

        this._renderer.OnImage(urlSpan, altTextSpan, ReadOnlySpan<char>.Empty);

        start = i;
        return true;
    }

    private bool TryParseEmphasis(ReadOnlySpan<char> text, ref int i, ref int start)
    {
        var emphStart = i;
        var emphChar = text[i];
        var emphCount = 0;

        // Count emphasis characters
        while (i < text.Length && text[i] == emphChar)
        {
            emphCount++;
            i++;
        }

        if (emphCount == 0)
        {
            return false;
        }

        var isStrong = emphCount >= 2;
        var searchCount = isStrong ? 2 : 1;

        // Find matching closing emphasis
        var closeIndex = i;
        while (closeIndex < text.Length)
        {
            if (text[closeIndex] != emphChar)
            {
                closeIndex++;
                continue;
            }

            var closeCount = 0;
            var tempIndex = closeIndex;

            while (tempIndex < text.Length && text[tempIndex] == emphChar && closeCount < searchCount)
            {
                closeCount++;
                tempIndex++;
            }

            if (closeCount == searchCount)
            {
                // Found matching close - emit text before emphasis
                if (emphStart > start)
                {
                    this._renderer.OnText(text[start..emphStart]);
                }

                // Extract emphasis text with span
                var emphTextSpan = text.Slice(i, closeIndex - i);

                if (isStrong)
                {
                    this._renderer.OnEnterStrongEmphasis();
                    if (emphTextSpan.Length > 0)
                    {
                        this.ParseInline(emphTextSpan);
                    }

                    this._renderer.OnExitStrongEmphasis();
                    i = tempIndex;
                }
                else
                {
                    this._renderer.OnEnterEmphasis();
                    if (emphTextSpan.Length > 0)
                    {
                        this.ParseInline(emphTextSpan);
                    }

                    this._renderer.OnExitEmphasis();
                    i = tempIndex;
                }

                start = i;
                return true;
            }

            closeIndex++;
        }

        // No matching close found
        i = emphStart + 1;
        return false;
    }

    #endregion

    /// <summary>
    /// Disposes of resources used by the parser.
    /// </summary>
    public void Dispose()
    {
        if (!this._disposed)
        {
            this._reader?.Dispose();
            this._disposed = true;
        }
    }
}
