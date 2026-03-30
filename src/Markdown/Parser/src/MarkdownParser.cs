using System.Text;
using System.Text.RegularExpressions;
using Femur.Parsing;
using Femur.Parsing.Nodes;
using Femur.Markdown.Abstractions.Nodes;
using Femur.Markdown.Parser.Compatibility;

namespace Femur.Markdown.Parser;

/// <summary>
/// Streaming Markdown parser that reads from a Stream and builds an AST.
/// Implements CommonMark 0.31.2 specification.
/// 
/// PARSING STRATEGY:
/// - Uses a sliding buffer to read stream in chunks (default 4KB)
/// - Tracks absolute position across buffer boundaries for location tracking
/// - Two-phase parsing approach per CommonMark spec:
///   1. Phase 1: Block structure (line-by-line parsing of blocks)
///   2. Phase 2: Inline structure (character-by-character parsing of inlines within blocks)
/// </summary>
public class MarkdownParser : StreamParser<MarkdownDocumentNode>
{
    /// <summary>
    /// Cached compiled regexes used in hot parsing paths.
    /// Nested inside MarkdownParser to satisfy SA1649 (first type in file must match filename).
    /// </summary>
    private static class Regexes
    {
        internal static readonly Regex BulletListLine =
            new Regex(@"^\s*[-*+]\s", RegexOptions.Compiled);

        internal static readonly Regex OrderedListLine =
            new Regex(@"^\s*\d+[.)]\s", RegexOptions.Compiled);

        internal static readonly Regex WhitespaceCollapse =
            new Regex(@"\s+", RegexOptions.Compiled);

        internal static readonly Regex HtmlBlock1Start =
            new Regex(@"^<(script|style|pre|iframe)(?:\s|>|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Block-6 tag list compiled once.
        // Per CommonMark 0.31.2 spec section 4.6, this includes: address, article, aside, base,
        // basefont, blockquote, body, caption, center, col, colgroup, dd, details, dialog, dir,
        // div, dl, dt, fieldset, figcaption, figure, footer, form, frame, frameset, h1-h6, head,
        // header, hr, html, iframe, legend, li, link, main, menu, menuitem, nav, noframes, ol,
        // optgroup, option, p, param, search, section, summary, table, tbody, td, tfoot, th,
        // thead, title, tr, track, ul
        private const string Block6Tags =
            "address|article|aside|base|basefont|blockquote|body|caption|center|col|colgroup|dd|" +
            "details|dialog|dir|div|dl|dt|fieldset|figcaption|figure|footer|form|frame|frameset|" +
            "h1|h2|h3|h4|h5|h6|head|header|hr|html|iframe|legend|li|link|main|menu|menuitem|meta|nav|" +
            "noframes|ol|optgroup|option|p|param|search|section|source|summary|table|tbody|td|tfoot|th|" +
            "thead|title|tr|track|ul";

        internal static readonly Regex HtmlBlock6Start =
            new Regex(@"^</?(" + Block6Tags + @")(?:\s|>|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static readonly Regex HtmlBlock7Start =
            new Regex(@"^</?\w+(?:\s|[^>]*)?/?>?\s*$", RegexOptions.Compiled);

        internal static readonly Regex HtmlBlock7ExcludeStart =
            new Regex(@"^<(script|pre|style)(?:\s|>|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
    private MarkdownDocumentNode? _document;
    private MarkdownContainerNode? _currentParent;
    private Stack<MarkdownContainerNode>? _blockStack;
    private List<string>? _lines;
    private StringBuilder? _currentLine;
    private Dictionary<string, LinkReferenceDefinition>? _linkReferences;
    private Dictionary<MarkdownContainerNode, string>? _originalTextMap; // Store original text before inline parsing

    /// <summary>
    /// Link reference definition for Phase 2 inline parsing
    /// </summary>
    private sealed class LinkReferenceDefinition
    {
        public string Url { get; set; } = string.Empty;
        public string? Title { get; set; }
    }

    /// <summary>
    /// Delimiter entry for the delimiter stack algorithm.
    /// Tracks potential opening and closing delimiters for emphasis, links, etc.
    /// </summary>
    private sealed class DelimiterEntry
    {
        /// <summary>The delimiter character: *, _, [, ], (, etc.</summary>
        public char Character { get; set; }

        /// <summary>Number of consecutive delimiter characters (1, 2, or 3 for emphasis).</summary>
        public int Count { get; set; }

        /// <summary>Starting index in the text where this delimiter sequence begins.</summary>
        public int StartIndex { get; set; }

        /// <summary>Ending index in the text where this delimiter sequence ends.</summary>
        public int EndIndex { get; set; }

        /// <summary>Whether this delimiter can open emphasis (left-flanking run).</summary>
        public bool CanOpen { get; set; }

        /// <summary>Whether this delimiter can close emphasis (right-flanking run).</summary>
        public bool CanClose { get; set; }

        public override string ToString() => $"{this.Character}x{this.Count} @{this.StartIndex} (Open:{this.CanOpen} Close:{this.CanClose})";
    }

    /// <summary>
    /// Full CommonMark 0.31.2 compliant delimiter processor (section 6.3).
    /// Implements the official CommonMark delimiter stack algorithm.
    /// </summary>
    private sealed class CommonMarkDelimiterProcessor
    {
        private readonly string _text;
        // List<T> used as a stack: push = Add, pop = RemoveAt(Count-1), peek = [Count-1].
        // Unlike Stack<T>, List<T> supports O(1) random-access indexing so we never need ToArray().
        private readonly List<DelimiterEntry> _delimiterStack = new List<DelimiterEntry>();
        private readonly List<(DelimiterEntry opener, DelimiterEntry closer, int emphasisLevel)> _processedMatches = new List<(DelimiterEntry, DelimiterEntry, int)>();

        public CommonMarkDelimiterProcessor(string text)
        {
            this._text = text;
        }

        /// <summary>
        /// Process text and return list of (start, end, type) tuples indicating which ranges are emphasis/links.
        /// </summary>
        public List<(int start, int end, string type)> ProcessDelimiters()
        {
            // Step 1: Identify all potential delimiters
            var delimiters = this.IdentifyAllDelimiters();

            // Step 2: Process delimiters according to CommonMark rules
            this.ProcessEmphasisDelimiters(delimiters);

            // Step 3: Convert processed matches to ranges
            var result = new List<(int start, int end, string type)>();
            foreach (var (opener, closer, emphasisLevel) in this._processedMatches)
            {
                result.Add((opener.StartIndex, closer.EndIndex, emphasisLevel == 1 ? "emphasis" : "strong"));
            }

            return result;
        }

        /// <summary>
        /// Identifies all potential emphasis delimiters (* and _).
        /// </summary>
        private List<DelimiterEntry> IdentifyAllDelimiters()
        {
            var delimiters = new List<DelimiterEntry>();

            for (var i = 0; i < this._text.Length; i++)
            {
                var ch = this._text[i];

                if (ch == '*' || ch == '_')
                {
                    // Count consecutive delimiters
                    var count = 1;
                    while (i + count < this._text.Length && this._text[i + count] == ch)
                    {
                        count++;
                    }

                    // Determine if left-flanking and right-flanking
                    var (canOpen, canClose) = this.DetermineFlanking(i, count, ch);

                    delimiters.Add(new DelimiterEntry
                    {
                        Character = ch,
                        Count = count,
                        StartIndex = i,
                        EndIndex = i + count - 1,
                        CanOpen = canOpen,
                        CanClose = canClose
                    });

                    i += count - 1; // Skip past the delimiter sequence
                }
            }

            return delimiters;
        }

        /// <summary>
        /// Determines if a delimiter run is left-flanking and/or right-flanking.
        /// Per CommonMark spec section 6.2.
        /// </summary>
        private (bool canOpen, bool canClose) DetermineFlanking(int startIdx, int count, char marker)
        {
            var endIdx = startIdx + count - 1;

            // Get preceding and following characters
            var charBefore = startIdx > 0 ? this._text[startIdx - 1] : '\0';
            var charAfter = endIdx < this._text.Length - 1 ? this._text[endIdx + 1] : '\0';

            var isLeftWhitespace = charBefore == '\0' || char.IsWhiteSpace(charBefore);
            var isRightWhitespace = charAfter == '\0' || char.IsWhiteSpace(charAfter);
            var isLeftPunctuation = charBefore != '\0' && this.IsPunctuation(charBefore);
            var isRightPunctuation = charAfter != '\0' && this.IsPunctuation(charAfter);

            // Left-flanking run: not followed by whitespace, and (preceded by whitespace or punctuation or start)
            var isLeftFlanking = !isRightWhitespace && (isLeftWhitespace || isLeftPunctuation);

            // Right-flanking run: not preceded by whitespace, and (followed by whitespace or punctuation or end)
            var isRightFlanking = !isLeftWhitespace && (isRightWhitespace || isRightPunctuation);

            // For * delimiters: can open if left-flanking, can close if right-flanking
            // For _ delimiters: additional restrictions based on punctuation
            var canOpen = isLeftFlanking && (marker == '*' || !isRightFlanking || isLeftPunctuation);
            var canClose = isRightFlanking && (marker == '*' || !isLeftFlanking || isRightPunctuation);

            return (canOpen, canClose);
        }

        /// <summary>
        /// Processes emphasis delimiters according to CommonMark algorithm.
        /// </summary>
        private void ProcessEmphasisDelimiters(List<DelimiterEntry> delimiters)
        {
            foreach (var closer in delimiters)
            {
                if (!closer.CanClose)
                {
                    continue;
                }

                // Walk the stack looking for matching opener.
                // _delimiterStack is a List<T> so we can index directly — no ToArray() needed.
                var openerIndex = -1;
                for (var i = this._delimiterStack.Count - 1; i >= 0; i--)
                {
                    var potential = this._delimiterStack[i];

                    if (!potential.CanOpen || potential.Character != closer.Character)
                    {
                        continue;
                    }

                    // Check if we can use this opener
                    var openerCount = potential.Count;
                    var closerCount = closer.Count;

                    // Determine how many delimiters are used
                    var useCount = Math.Min(openerCount, closerCount);
                    if (useCount == 0)
                    {
                        continue;
                    }

                    // For * and _ with count >= 2: use 2, else use 1
                    if (useCount >= 2)
                    {
                        useCount = 2;
                    }
                    else
                    {
                        useCount = 1;
                    }

                    // Found matching opener - but only if there's at least one valid match
                    if (openerCount >= useCount && closerCount >= useCount)
                    {
                        openerIndex = i;
                        break;
                    }
                }

                if (openerIndex >= 0)
                {
                    var opener = this._delimiterStack[openerIndex];

                    var useCount = Math.Min(opener.Count, closer.Count);
                    if (useCount >= 2)
                    {
                        useCount = 2;
                    }
                    else
                    {
                        useCount = 1;
                    }

                    // Record the match
                    this._processedMatches.Add((opener, closer, useCount));

                    // Remove matched opener and all unopened delimiters after it
                    // (RemoveRange is O(n) but avoids repeated RemoveAt shifts for large stacks)
                    this._delimiterStack.RemoveRange(openerIndex, this._delimiterStack.Count - openerIndex);

                    // If opener wasn't fully consumed, push back the remainder
                    if (opener.Count > useCount)
                    {
                        this._delimiterStack.Add(new DelimiterEntry
                        {
                            Character = opener.Character,
                            Count = opener.Count - useCount,
                            StartIndex = opener.StartIndex + useCount,
                            EndIndex = opener.EndIndex,
                            CanOpen = opener.CanOpen,
                            CanClose = opener.CanClose
                        });
                    }

                    // If closer wasn't fully consumed, continue processing from here
                    if (closer.Count > useCount)
                    {
                        // Push remaining closer back for next iteration
                        closer.Count -= useCount;
                        closer.StartIndex += useCount;
                    }
                    else
                    {
                        // Closer fully consumed, continue to next delimiter
                        break;
                    }
                }
                else
                {
                    // No matching opener found
                    if (closer.CanOpen)
                    {
                        this._delimiterStack.Add(closer);
                    }
                }
            }
        }

        private bool IsPunctuation(char ch)
        {
            // CommonMark punctuation character: in general Unicode categories Pc, Pd, Pe, Pf, Pi, Po, or Ps
            var category = char.GetUnicodeCategory(ch);
            return category >= System.Globalization.UnicodeCategory.ConnectorPunctuation &&
                   category <= System.Globalization.UnicodeCategory.OtherPunctuation;
        }
    }

    /// <summary>
    /// Creates a new Markdown parser for the given stream
    /// </summary>
    /// <param name="stream">The stream to parse</param>
    /// <param name="bufferSize">Size of the buffer for reading chunks (default 4096)</param>
    /// <param name="leaveOpen">true to leave the stream open after the parser is disposed; otherwise, false (default false)</param>
    public MarkdownParser(Stream stream, int bufferSize = 4096, bool leaveOpen = false) : base(stream, bufferSize, leaveOpen)
    {
    }

    /// <summary>
    /// Creates a new document instance
    /// </summary>
    protected override MarkdownDocumentNode CreateDocument()
    {
        return new MarkdownDocumentNode();
    }

    /// <summary>
    /// Initializes parsing state (stacks, flags, etc.)
    /// </summary>
    protected override void InitializeParsing(MarkdownDocumentNode document)
    {
        this._document = document;
        this._currentParent = document;
        this._blockStack = new Stack<MarkdownContainerNode>();
        this._lines = new List<string>();
        this._currentLine = new StringBuilder();
        this._linkReferences = new Dictionary<string, LinkReferenceDefinition>(StringComparer.OrdinalIgnoreCase);
        this._originalTextMap = new Dictionary<MarkdownContainerNode, string>();
    }

    /// <summary>
    /// Processes a single character from the stream.
    /// Accumulates characters into lines for Phase 1 block parsing.
    /// </summary>
    protected override void ProcessCharacter(char ch, MarkdownDocumentNode document)
    {
        // Note: ch is a fully decoded Unicode char. StreamParser uses a StreamReader backed by
        // UTF-8 encoding which decodes multi-byte sequences before writing into the char[] Buffer.
        // All Unicode characters (including box-drawing, emoji, CJK, etc.) survive intact.

        if (ch == '\n' || ch == '\r')
        {
            // End of line - add to lines list
            if (this._currentLine != null && (this._currentLine.Length > 0 || (this._lines != null && this._lines.Count == 0)))
            {
                this._lines!.Add(this._currentLine.ToString());
                _ = this._currentLine.Clear();
            }
            else
            {
                // Empty line
                this._lines!.Add(string.Empty);
            }

            // Handle \r\n - advance past both characters so the \n is not
            // processed again as a second (spurious) empty line on Windows.
            // this.Position still points at the current char (\r or \n) here,
            // so we check Position+1 for the paired \n after a \r.
            if (ch == '\r' && this.Position + 1 < this.Length && this.Buffer[this.Position + 1] == '\n')
            {
                this.Position++; // skip the \n that follows \r
            }

            this.Position++; // advance past \r (or the lone \n)
        }
        else
        {
            _ = this._currentLine!.Append(ch);
            this.Position++; // Advance past the character
        }
    }

    /// <summary>
    /// Cleanup after parsing is complete.
    /// Processes accumulated lines for Phase 1 (block structure), then Phase 2 (inline structure).
    /// </summary>
    protected override void Cleanup()
    {
        // Add final line if not empty
        if (this._currentLine!.Length > 0)
        {
            this._lines!.Add(this._currentLine.ToString());
        }

        // Phase 1: Parse block structure
        this.ParseBlockStructure();

        // Phase 2: Parse inline structure
        this.ParseInlineStructure();

        // Phase 3: Apply smart punctuation
        this.ApplySmartPunctuation();

        base.Cleanup();
    }

    #region Phase 1: Block Structure Parsing

    /// <summary>
    /// Phase 1: Parse block structure line-by-line per CommonMark spec
    /// </summary>
    private void ParseBlockStructure()
    {
        if (this._lines == null || this._document == null)
        {
            return;
        }

        var i = 0;
        this.ParseBlockStructureRange(ref i);
    }

    /// <summary>
    /// Core block-structure parsing loop. Reads from this._lines starting at <paramref name="startIndex"/>
    /// and adds parsed block nodes to this._currentParent. Callers set up _lines and _currentParent
    /// before calling, and restore them afterwards.
    /// </summary>
    private void ParseBlockStructureRange(ref int startIndex)
    {
        var i = startIndex;
        while (i < this._lines!.Count)
        {
            var line = this._lines[i];
            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;

            // Check for indented code blocks BEFORE skipping blank lines
            // (lines with 4+ spaces are code blocks, even if they're just whitespace)
            if (indent >= 4)
            {
                if (this.TryParseIndentedCodeBlock(line, indent, i, ref i, out var codeBlock) && codeBlock != null)
                {
                    this.AddBlock(codeBlock);
                    continue;
                }
            }

            // Skip blank lines (but only if not indented code blocks)
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                i++;
                continue;
            }

            // Check for block-level constructs
            // Note: Setext headings are checked by looking ahead after paragraphs
            if (this.TryParseAtxHeading(trimmed, i, out var heading) && heading != null)
            {
                this.AddBlock(heading);
                i++;
            }
            else if (this.TryParseFencedDiv(trimmed, i, ref i, out var fencedDiv) && fencedDiv != null)
            {
                this.AddBlock(fencedDiv);
            }
            else if (this.TryParseFencedCodeBlock(trimmed, i, ref i, out var codeBlock) && codeBlock != null)
            {
                this.AddBlock(codeBlock);
            }
            else if (this.TryParseBlockQuote(trimmed, i, ref i, out var blockQuote) && blockQuote != null)
            {
                this.AddBlock(blockQuote);
            }
            else if (this.TryParseList(trimmed, indent, i, ref i, out var list) && list != null)
            {
                this.AddBlock(list);
            }
            else if (this.TryParseLinkReferenceDefinition(trimmed, i, ref i, out _))
            {
                // Link reference definitions are consumed but not added to tree
                // i is already advanced by TryParseLinkReferenceDefinition if multi-line
            }
            else if (this.TryParseHtmlBlock(trimmed, i, ref i, out var htmlBlock) && htmlBlock != null)
            {
                this.AddBlock(htmlBlock);
            }
            else if (this.TryParseThematicBreak(trimmed, i, out var thematicBreak) && thematicBreak != null)
            {
                // Thematic break is checked AFTER other constructs but BEFORE paragraphs
                // This ensures that a line that could be a Setext underline is only treated as
                // a thematic break if it's not immediately after a paragraph
                // However, if we get here, there was no paragraph before it, so it's truly a thematic break
                this.AddBlock(thematicBreak);
                i++;
            }
            else
            {
                // Regular paragraph - but first check if next line is a Setext underline
                // If so, parse as Setext heading instead
                if (i + 1 < this._lines!.Count)
                {
                    var nextLine = this._lines[i + 1].Trim();
                    if (this.IsSetextUnderline(nextLine))
                    {
                        // This is a Setext heading - parse first line as paragraph, then convert
                        var paragraph = this.ParseParagraph(trimmed, i, ref i);
                        if (paragraph != null)
                        {
                            // Convert paragraph to Setext heading
                            var marker = nextLine[0];
                            var level = marker == '=' ? 1 : 2;
                            var setextHeading = new HeadingNode
                            {
                                Level = level,
                                Location = paragraph.Location
                            };

                            // Move paragraph children to heading
                            var pChildren = paragraph.Children;
                            for (var ci = 0; ci < pChildren.Count; ci++)
                            {
                                var child = pChildren[ci];
                                setextHeading.Children.Add(child);
                                child.SetParent(setextHeading);
                            }

                            this.AddBlock(setextHeading);
                            i++; // Skip underline line
                            continue;
                        }
                    }
                }

                // Regular paragraph
                var regularParagraph = this.ParseParagraph(trimmed, i, ref i);
                if (regularParagraph != null)
                {
                    this.AddBlock(regularParagraph);
                }
            }
        }

        startIndex = i;
    }

    private void AddBlock(Node node)
    {
        node.SetParent(this._currentParent);
        this._currentParent?.Children.Add(node);
    }

    private bool TryParseAtxHeading(string line, int lineIndex, out HeadingNode? heading)
    {
        heading = null;
        if (!line.StartsWith('#'))
        {
            return false;
        }

        var level = 0;
        var i = 0;
        while (i < line.Length && i < 6 && line[i] == '#')
        {
            level++;
            i++;
        }

        if (level == 0 || level > 6)
        {
            return false;
        }

        // Skip whitespace after #
        while (i < line.Length && char.IsWhiteSpace(line[i]))
        {
            i++;
        }

        // Read heading text (remove trailing #s)
        var text = line.Substring(i).TrimEnd('#').TrimEnd();

        heading = new HeadingNode
        {
            Level = level,
            Location = new SourceLocation(0, line.Length, lineIndex + 1, 1)
        };

        // Store raw text for Phase 2 inline parsing
        heading.Children.Add(new MarkdownTextNode { Content = text });

        return true;
    }

    private bool IsSetextUnderline(string line)
    {
        // CommonMark spec: Setext underline can have tabs/spaces
        // Trim but preserve the fact that it's whitespace + markers
        // The underline can be any length (even 1 character)
        var trimmed = line.Trim();
        if (trimmed.Length < 1)
        {
            return false;
        }

        var marker = trimmed[0];
        if (marker != '=' && marker != '-')
        {
            return false;
        }

        // All characters must be the same marker (with optional spaces/tabs)
        // At least one marker must be present
        var markerCount = 0;
        foreach (var c in trimmed)
        {
            if (c == marker)
            {
                markerCount++;
            }
            else if (!char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return markerCount >= 1;
    }

    private bool TryParseThematicBreak(string line, int lineIndex, out ThematicBreakNode? thematicBreak)
    {
        thematicBreak = null;
        var trimmed = line.Trim();
        if (trimmed.Length < 3)
        {
            return false;
        }

        var marker = trimmed[0];
        if (marker != '-' && marker != '*' && marker != '_')
        {
            return false;
        }

        // All characters must be the same marker (with optional spaces)
        var count = 0;
        foreach (var ch in trimmed)
        {
            if (ch == marker)
            {
                count++;
            }
            else if (!char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        if (count < 3)
        {
            return false;
        }

        thematicBreak = new ThematicBreakNode
        {
            Location = new SourceLocation(0, line.Length, lineIndex + 1, 1)
        };

        return true;
    }

    /// <summary>
    /// Checks if a line is a thematic break (at least 3 of the same marker with optional spaces).
    /// </summary>
    private bool IsThematicBreakLine(string line, char marker)
    {
        if (line.Length < 3)
        {
            return false;
        }

        if (marker != '-' && marker != '*' && marker != '_')
        {
            return false;
        }

        // All characters must be the same marker (with optional spaces/tabs)
        var count = 0;
        foreach (var ch in line)
        {
            if (ch == marker)
            {
                count++;
            }
            else if (!char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        return count >= 3;
    }

    private bool TryParseFencedCodeBlock(string line, int lineIndex, ref int currentIndex, out CodeBlockNode? codeBlock)
    {
        codeBlock = null;

        // Check for opening fence: ``` or ~~~
        var fenceChar = line.Length > 0 ? line[0] : '\0';
        if (fenceChar != '`' && fenceChar != '~')
        {
            return false;
        }

        var fenceLength = 0;
        while (fenceLength < line.Length && line[fenceLength] == fenceChar)
        {
            fenceLength++;
        }

        if (fenceLength < 3)
        {
            return false;
        }

        // Read info string (optional)
        var info = line.Substring(fenceLength).Trim();

        // Read code content until closing fence
        var content = new StringBuilder();
        currentIndex++;

        while (currentIndex < this._lines!.Count)
        {
            var currentLine = this._lines[currentIndex];
            if (currentLine.Length >= fenceLength)
            {
                var isClosingFence = true;
                for (var i = 0; i < fenceLength; i++)
                {
                    if (currentLine[i] != fenceChar)
                    {
                        isClosingFence = false;
                        break;
                    }
                }

                if (isClosingFence)
                {
                    // Found closing fence
                    currentIndex++;
                    break;
                }
            }

            if (content.Length > 0)
            {
                _ = content.Append('\n');
            }

            _ = content.Append(currentLine);
            currentIndex++;
        }

        codeBlock = new CodeBlockNode
        {
            Content = content.ToString(),
            Info = string.IsNullOrWhiteSpace(info) ? null : info,
            IsFenced = true,
            Location = new SourceLocation(0, content.Length, lineIndex + 1, 1)
        };

        return true;
    }

    /// <summary>
    /// Attempts to parse a fenced div (container block delimited by ::: markers).
    /// Implements the Pandoc fenced_divs extension.
    /// </summary>
    private bool TryParseFencedDiv(string line, int lineIndex, ref int currentIndex, out FencedDivNode? fencedDiv)
    {
        fencedDiv = null;

        // Check for opening fence: ::: with at least 3 colons followed by attributes
        if (line.Length == 0 || line[0] != ':')
        {
            return false;
        }

        // Count leading colons
        var fenceLength = 0;
        while (fenceLength < line.Length && line[fenceLength] == ':')
        {
            fenceLength++;
        }

        if (fenceLength < 3)
        {
            return false;
        }

        // Extract everything after the opening colons
        var rest = line.Substring(fenceLength).TrimStart();

        // Check if this is a closing fence (no content after colons)
        if (string.IsNullOrWhiteSpace(rest))
        {
            return false;
        }

        // Parse name and attributes
        // Format can be:
        // - :::name {attributes} - named div with attributes
        // - :::name - named div without attributes
        // - ::: {attributes} - unnamed div with attributes (original Pandoc format)
        string? name = null;
        var attributes = string.Empty;

        // Check if rest starts with '{' (attributes without name)
        if (rest.StartsWith('{'))
        {
            // Unnamed div with attributes: ::: {attributes}
            attributes = rest.Trim();
        }
        else
        {
            // Check if there's a name followed by attributes
            var spaceIndex = rest.IndexOf(' ');
            if (spaceIndex > 0)
            {
                // Has space - check if next part is attributes
                name = rest.Substring(0, spaceIndex).Trim();
                var afterSpace = rest.Substring(spaceIndex).TrimStart();

                if (afterSpace.StartsWith('{'))
                {
                    // Named div with attributes: :::name {attributes}
                    attributes = afterSpace.Trim();
                }
                else
                {
                    // Name with space but no attributes - treat name as including the space content
                    // This handles cases like :::name content (no attributes)
                    name = rest.Trim();
                    attributes = string.Empty;
                }
            }
            else
            {
                // No space - could be just name or name with no attributes
                // Check if it looks like attributes (starts with {)
                if (rest.StartsWith('{'))
                {
                    attributes = rest.Trim();
                }
                else
                {
                    // Named div without attributes: :::name
                    name = rest.Trim();
                    attributes = string.Empty;
                }
            }
        }

        // For backward compatibility, allow unnamed divs without attributes only if they have attributes
        // But named divs can exist without attributes
        if (string.IsNullOrEmpty(name) && string.IsNullOrWhiteSpace(attributes))
        {
            return false;
        }

        // Read div content until closing fence
        // Track nesting depth to handle nested fenced divs correctly
        var divContent = new List<string>();
        currentIndex++;
        var closingFenceFound = false;
        var nestingDepth = 0;

        while (currentIndex < this._lines!.Count)
        {
            var currentLine = this._lines[currentIndex];
            var trimmedLine = currentLine.Trim();

            // Check for fenced div markers (opening or closing)
            if (trimmedLine.Length > 0 && trimmedLine[0] == ':')
            {
                var colonCount = 0;
                while (colonCount < trimmedLine.Length && trimmedLine[colonCount] == ':')
                {
                    colonCount++;
                }

                if (colonCount >= 3)
                {
                    // Check what comes after the colons
                    var afterColons = trimmedLine.Substring(colonCount).Trim();

                    // Check if this is an opening fence (has attributes or name)
                    // Opening fence can be:
                    // - :::name {attributes}
                    // - :::name
                    // - ::: {attributes}
                    if (!string.IsNullOrEmpty(afterColons) && !afterColons.All(c => c == ':'))
                    {
                        // Check if it's an opening fence (has name or attributes starting with {)
                        var isOpeningFence = afterColons.StartsWith('{') ||
                                             (!string.IsNullOrWhiteSpace(afterColons) && !afterColons.Trim().StartsWith('{') && afterColons.IndexOf(' ') < 0);

                        // More precise check: if it doesn't start with {, check if it has a space followed by {
                        if (!isOpeningFence && afterColons.Contains(' '))
                        {
                            var spaceIdx = afterColons.IndexOf(' ');
                            var afterSpace = afterColons.Substring(spaceIdx).TrimStart();
                            isOpeningFence = afterSpace.StartsWith('{');
                        }

                        if (isOpeningFence)
                        {
                            // This is a nested opening fence - increase nesting depth
                            nestingDepth++;
                            divContent.Add(currentLine);
                            currentIndex++;
                            continue;
                        }
                    }
                    else
                    {
                        // This is a closing fence
                        if (nestingDepth > 0)
                        {
                            // This closes a nested div, not the outer one
                            nestingDepth--;
                            divContent.Add(currentLine);
                            currentIndex++;
                            continue;
                        }
                        else
                        {
                            // This closes the outer div
                            closingFenceFound = true;
                            currentIndex++;
                            break;
                        }
                    }
                }
            }

            divContent.Add(currentLine);
            currentIndex++;
        }

        // Only treat as valid fenced div if we found a closing fence
        if (!closingFenceFound)
        {
            // Reset and treat as regular content
            currentIndex = lineIndex + 1;
            return false;
        }

        // Parse the div content recursively (it can contain any blocks)
        // Tags with colons (e.g., "C:Codeblock") are treated as literal content containers
        // and skip inner markdown parsing
        var shouldParseContent = string.IsNullOrEmpty(name) || (name != null && !name.Contains(':'));

        // Only compute the raw content string when needed (either for storage or literal tags).
        var contentStr = shouldParseContent ? null : StringCompat.Join('\n', divContent);

        fencedDiv = new FencedDivNode
        {
            Tag = name,
            Attributes = attributes,
            OpeningFenceLength = fenceLength,
            RawContent = contentStr ?? string.Empty,
            Location = new SourceLocation(0, divContent.Count, lineIndex + 1, 1)
        };

        // Only parse inner content for non-literal tags (tags without colons).
        // Reuse the current parser state (swap _lines) instead of spawning a new MarkdownParser
        // instance + MemoryStream + UTF-8 encode/decode round-trip.
        if (shouldParseContent)
        {
            var savedLines = this._lines;
            var savedParent = this._currentParent;
            var savedIndex = currentIndex; // already past the closing fence

            this._lines = divContent;
            this._currentParent = fencedDiv;
            var innerIndex = 0;

            // Run the same block-structure loop used at the document level.
            this.ParseBlockStructureRange(ref innerIndex);

            this._lines = savedLines;
            this._currentParent = savedParent;
            // currentIndex is already correct (was set before we entered this branch).
        }

        // Parse attributes
        fencedDiv.ParsedAttributes = this.ParseDivAttributes(attributes);

        return true;
    }

    /// <summary>
    /// Parses fenced div attributes in Pandoc format: {#id .class1 .class2 key=value}
    /// </summary>
    private FencedDivAttributes ParseDivAttributes(string attributeString)
    {
        var result = new FencedDivAttributes();

        if (string.IsNullOrWhiteSpace(attributeString))
        {
            return result;
        }

        // Remove outer braces if present
        var attrs = attributeString.Trim();
        if (attrs.Length > 0 && attrs[0] == '{' && attrs[attrs.Length - 1] == '}')
        {
            attrs = attrs.Substring(1, attrs.Length - 2).Trim();
        }

        // Simple tokenization: split by spaces outside of quotes
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in attrs)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                current.Append(ch);
            }
            else if (ch == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        // Parse tokens
        foreach (var token in tokens)
        {
            if (token.StartsWith('#'))
            {
                // ID
                result.Id = token.Substring(1);
            }
            else if (token.StartsWith('.'))
            {
                // Class
                result.Classes.Add(token.Substring(1));
            }
            else if (token.Contains('='))
            {
                // Key=value attribute
                var equalsIndex = token.IndexOf('=');
                if (equalsIndex > 0)
                {
                    var key = token.Substring(0, equalsIndex);
                    var value = token.Substring(equalsIndex + 1);

                    // Remove surrounding quotes if present
                    if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    result.KeyValueAttributes[key] = value;
                }
            }
        }

        return result;
    }

    private bool TryParseIndentedCodeBlock(string line, int indent, int lineIndex, ref int currentIndex, out CodeBlockNode? codeBlock)
    {
        codeBlock = null;

        // Indented code block requires 4+ spaces
        if (indent < 4)
        {
            return false;
        }

        var content = new StringBuilder();

        while (currentIndex < this._lines!.Count)
        {
            var currentLine = this._lines[currentIndex];
            var currentIndent = currentLine.Length - currentLine.TrimStart().Length;

            // Blank lines are part of code block
            if (string.IsNullOrWhiteSpace(currentLine))
            {
                // Always add newline to preserve structure, even if content is empty
                _ = content.Append('\n');
                currentIndex++;
                continue;
            }

            // Code block continues if line has 4+ spaces indent
            if (currentIndent >= 4)
            {
                if (content.Length > 0)
                {
                    _ = content.Append('\n');
                }

                // Remove 4 spaces of indentation
                var remainingContent = currentLine.Substring(4);
                _ = content.Append(remainingContent);

                currentIndex++;
            }
            else
            {
                // End of code block
                break;
            }
        }

        // Always create code block, even if empty (CommonMark allows empty code blocks)
        codeBlock = new CodeBlockNode
        {
            Content = content.ToString(),
            IsFenced = false,
            Location = new SourceLocation(0, content.Length, lineIndex + 1, 1)
        };

        return true;
    }

    private bool TryParseBlockQuote(string line, int lineIndex, ref int currentIndex, out BlockQuoteNode? blockQuote)
    {
        blockQuote = null;

        if (!line.StartsWith('>'))
        {
            return false;
        }

        blockQuote = new BlockQuoteNode
        {
            Location = new SourceLocation(0, 0, lineIndex + 1, 1)
        };

        var savedParent = this._currentParent;
        this._currentParent = blockQuote;

        // Parse content lines (may include lazy continuation)
        var contentLines = new List<string>();
        while (currentIndex < this._lines!.Count)
        {
            var currentLine = this._lines[currentIndex];
            var trimmed = currentLine.TrimStart();

            if (trimmed.StartsWith('>'))
            {
                // Remove > and optional space
                var content = trimmed.Substring(1).TrimStart();
                contentLines.Add(content);
                currentIndex++;
            }
            else if (string.IsNullOrWhiteSpace(trimmed))
            {
                // Blank line ends blockquote
                break;
            }
            else if (contentLines.Count > 0 && trimmed.Length > 0 && trimmed[0] != '>' && this._currentParent == blockQuote)
            {
                // Lazy continuation - part of blockquote
                contentLines.Add(trimmed);
                currentIndex++;
            }
            else
            {
                break;
            }
        }

        // Parse content as blocks recursively
        // Create a temporary document-like structure to parse nested blocks
        var savedLines = this._lines;
        var savedIndex = currentIndex;
        this._lines = contentLines;
        currentIndex = 0;

        // Parse blocks from content lines
        while (currentIndex < contentLines.Count)
        {
            var contentLine = contentLines[currentIndex];
            var contentTrimmed = contentLine.TrimStart();
            var contentIndent = contentLine.Length - contentTrimmed.Length;

            if (string.IsNullOrWhiteSpace(contentTrimmed))
            {
                currentIndex++;
                continue;
            }

            // Try parsing different block types
            if (this.TryParseAtxHeading(contentTrimmed, currentIndex, out var heading) && heading != null)
            {
                this.AddBlock(heading);
                currentIndex++;
            }
            else if (this.TryParseFencedCodeBlock(contentTrimmed, currentIndex, ref currentIndex, out var codeBlock) && codeBlock != null)
            {
                this.AddBlock(codeBlock);
            }
            else if (this.TryParseIndentedCodeBlock(contentTrimmed, contentIndent, currentIndex, ref currentIndex, out codeBlock) && codeBlock != null)
            {
                this.AddBlock(codeBlock);
            }
            else if (this.TryParseList(contentTrimmed, contentIndent, currentIndex, ref currentIndex, out var list) && list != null)
            {
                this.AddBlock(list);
            }
            else if (this.TryParseBlockQuote(contentTrimmed, currentIndex, ref currentIndex, out var nestedQuote) && nestedQuote != null)
            {
                this.AddBlock(nestedQuote);
            }
            else
            {
                // Parse as paragraph
                var paragraph = this.ParseParagraph(contentTrimmed, currentIndex, ref currentIndex);
                if (paragraph != null)
                {
                    this.AddBlock(paragraph);
                }
            }
        }

        // Restore original state
        this._lines = savedLines;
        currentIndex = savedIndex;
        this._currentParent = savedParent;

        return true;
    }

    private bool TryParseList(string line, int indent, int lineIndex, ref int currentIndex, out ListNode? list)
    {
        list = null;

        // Check for list marker
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var isOrdered = false;
        var startNumber = 1;
        var bulletChar = '\0';
        var markerLength = 0;

        // Check for ordered list: number followed by . or )
        if (char.IsDigit(trimmed[0]))
        {
            var numberEnd = 0;
            while (numberEnd < trimmed.Length && char.IsDigit(trimmed[numberEnd]))
            {
                numberEnd++;
            }

            if (numberEnd < trimmed.Length && (trimmed[numberEnd] == '.' || trimmed[numberEnd] == ')'))
            {
                isOrdered = true;
                if (!Int32Compat.TryParse(trimmed.AsSpan(0, numberEnd), out startNumber))
                {
                    startNumber = 1;
                }

                markerLength = numberEnd + 1;
            }
        }

        // Check for unordered list: -, *, or +
        if (!isOrdered)
        {
            if (trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+')
            {
                bulletChar = trimmed[0];
                markerLength = 1;
            }
            else
            {
                return false;
            }
        }

        // Must be followed by space or tab
        if (markerLength >= trimmed.Length || !char.IsWhiteSpace(trimmed[markerLength]))
        {
            return false;
        }

        // Check if this is actually a thematic break (e.g., "* * *", "- - -")
        // A thematic break requires at least 3 of the same character with only spaces between them
        if (!isOrdered && this.IsThematicBreakLine(trimmed, bulletChar))
        {
            return false; // Not a list, it's a thematic break
        }

        list = new ListNode
        {
            IsOrdered = isOrdered,
            StartNumber = startNumber,
            BulletChar = bulletChar,
            Location = new SourceLocation(0, 0, lineIndex + 1, 1)
        };

        var savedParent = this._currentParent;
        this._currentParent = list;

        // Track if we've seen a blank line between items (for tight/loose detection)
        var hasBlankLineBetweenItems = false;
        var blankLineCount = 0;

        // Parse list items
        while (currentIndex < this._lines!.Count)
        {
            var currentLine = this._lines[currentIndex];
            var currentTrimmed = currentLine.TrimStart();
            var currentIndent = currentLine.Length - currentTrimmed.Length;

            // Check if this line starts a new list item
            var isListItem = false;
            if (currentTrimmed.Length > 0)
            {
                if (isOrdered && char.IsDigit(currentTrimmed[0]))
                {
                    var numEnd = 0;
                    while (numEnd < currentTrimmed.Length && char.IsDigit(currentTrimmed[numEnd]))
                    {
                        numEnd++;
                    }

                    if (numEnd < currentTrimmed.Length && (currentTrimmed[numEnd] == '.' || currentTrimmed[numEnd] == ')'))
                    {
                        // Check for whitespace after marker
                        var markerEnd = numEnd + 1;
                        if (markerEnd < currentTrimmed.Length && char.IsWhiteSpace(currentTrimmed[markerEnd]))
                        {
                            isListItem = true;
                        }
                    }
                }
                else if (!isOrdered && (currentTrimmed[0] == '-' || currentTrimmed[0] == '*' || currentTrimmed[0] == '+'))
                {
                    // Check for whitespace after marker
                    if (currentTrimmed.Length > 1 && char.IsWhiteSpace(currentTrimmed[1]))
                    {
                        isListItem = true;
                    }
                }
            }

            if (isListItem && currentIndent == indent)
            {
                // If we have blank lines before this item (and we already have items), mark as loose
                if (blankLineCount > 0 && list.Children.Count > 0)
                {
                    hasBlankLineBetweenItems = true;
                }

                blankLineCount = 0; // Reset blank line counter

                // New list item
                var listItem = this.ParseListItem(currentLine, currentIndex, ref currentIndex);
                if (listItem != null)
                {
                    listItem.SetParent(list);
                    list.Children.Add(listItem);
                }
            }
            else if (string.IsNullOrWhiteSpace(currentTrimmed))
            {
                // Blank line - may continue list or end it
                blankLineCount++;
                currentIndex++;
            }
            else
            {
                // Not a list item - end of list
                break;
            }
        }

        // Set IsLoose if we found blank lines between items
        list.IsLoose = hasBlankLineBetweenItems;

        this._currentParent = savedParent;

        return true;
    }

    private ListItemNode? ParseListItem(string line, int lineIndex, ref int currentIndex)
    {
        var trimmed = line.TrimStart();
        var indent = line.Length - trimmed.Length;

        // Find marker
        var markerEnd = 0;
        if (char.IsDigit(trimmed[0]))
        {
            while (markerEnd < trimmed.Length && char.IsDigit(trimmed[markerEnd]))
            {
                markerEnd++;
            }

            if (markerEnd < trimmed.Length && (trimmed[markerEnd] == '.' || trimmed[markerEnd] == ')'))
            {
                markerEnd++;
            }
        }
        else if (trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+')
        {
            markerEnd = 1;
        }

        // Skip whitespace after marker
        var contentStart = markerEnd;
        while (contentStart < trimmed.Length && char.IsWhiteSpace(trimmed[contentStart]))
        {
            contentStart++;
        }

        var listItem = new ListItemNode
        {
            Location = new SourceLocation(0, 0, lineIndex + 1, 1)
        };

        var contentLines = new List<string> { trimmed.Substring(contentStart) };
        currentIndex++;

        // Parse continuation lines (indented content)
        while (currentIndex < this._lines!.Count)
        {
            var currentLine = this._lines[currentIndex];
            var currentTrimmed = currentLine.TrimStart();
            var currentIndent = currentLine.Length - currentTrimmed.Length;

            if (string.IsNullOrWhiteSpace(currentTrimmed))
            {
                // Blank line - may be part of list item or end it
                // Look ahead to see if next line is indented (continuation)
                if (currentIndex + 1 < this._lines!.Count)
                {
                    var nextLine = this._lines[currentIndex + 1];
                    var nextTrimmed = nextLine.TrimStart();
                    var nextIndent = nextLine.Length - nextTrimmed.Length;

                    if (!string.IsNullOrWhiteSpace(nextTrimmed) && nextIndent > indent)
                    {
                        // Next line is indented - blank line is part of content
                        contentLines.Add(string.Empty);
                        currentIndex++;
                    }
                    else
                    {
                        // Blank line(s) between items - don't consume
                        break;
                    }
                }
                else
                {
                    // End of input - don't consume trailing blank
                    break;
                }
            }
            else if (currentIndent > indent)
            {
                // Indented content - continuation of list item
                contentLines.Add(currentTrimmed);
                currentIndex++;
            }
            else
            {
                // Not indented enough - end of list item
                break;
            }
        }

        // Parse content as blocks if indented, otherwise as paragraph
        var savedParent = this._currentParent;
        this._currentParent = listItem;

        // Check if content starts with block-level constructs
        var hasBlocks = false;
        foreach (var contentLine in contentLines)
        {
            var lineTrimmed = contentLine.TrimStart();
            if (string.IsNullOrWhiteSpace(lineTrimmed))
            {
                continue;
            }

            // Check for block-level constructs
            if (lineTrimmed.StartsWith('#') ||
                lineTrimmed.StartsWith('>') ||
                lineTrimmed.StartsWith("```") ||
                lineTrimmed.StartsWith("~~~") ||
                (lineTrimmed.Length >= 4 && lineTrimmed.Substring(0, 4) == "    ") ||
                Regexes.BulletListLine.IsMatch(lineTrimmed) ||
                Regexes.OrderedListLine.IsMatch(lineTrimmed))
            {
                hasBlocks = true;
                break;
            }
        }

        if (hasBlocks)
        {
            // Parse as blocks recursively
            var savedLines = this._lines;
            var savedIndex = currentIndex;
            this._lines = contentLines;
            currentIndex = 0;

            while (currentIndex < contentLines.Count)
            {
                var contentLine = contentLines[currentIndex];
                var contentTrimmed = contentLine.TrimStart();
                var contentIndent = contentLine.Length - contentTrimmed.Length;

                if (string.IsNullOrWhiteSpace(contentTrimmed))
                {
                    currentIndex++;
                    continue;
                }

                // Try parsing different block types
                if (this.TryParseFencedCodeBlock(contentTrimmed, currentIndex, ref currentIndex, out var codeBlock) && codeBlock != null)
                {
                    this.AddBlock(codeBlock);
                }
                else if (this.TryParseIndentedCodeBlock(contentTrimmed, contentIndent, currentIndex, ref currentIndex, out codeBlock) && codeBlock != null)
                {
                    this.AddBlock(codeBlock);
                }
                else if (this.TryParseBlockQuote(contentTrimmed, currentIndex, ref currentIndex, out var blockQuote) && blockQuote != null)
                {
                    this.AddBlock(blockQuote);
                }
                else if (this.TryParseList(contentTrimmed, contentIndent, currentIndex, ref currentIndex, out var nestedList) && nestedList != null)
                {
                    this.AddBlock(nestedList);
                }
                else
                {
                    // Parse as paragraph
                    var paragraph = this.ParseParagraph(contentTrimmed, currentIndex, ref currentIndex);
                    if (paragraph != null)
                    {
                        this.AddBlock(paragraph);
                    }
                }
            }

            // Restore original state
            this._lines = savedLines;
            currentIndex = savedIndex;
        }
        else
        {
            // Create paragraph from content
            var content = StringCompat.Join('\n', contentLines).TrimEnd();
            if (!string.IsNullOrWhiteSpace(content))
            {
                var paragraph = new ParagraphNode();
                paragraph.Children.Add(new MarkdownTextNode { Content = content });
                paragraph.SetParent(listItem);
                listItem.Children.Add(paragraph);
            }
        }

        this._currentParent = savedParent;
        return listItem;
    }

    private bool TryParseLinkReferenceDefinition(string line, int lineIndex, ref int currentIndex, out LinkReferenceDefinition? definition)
    {
        definition = null;

        // Format: [id]: url "title" or [id]: url 'title' or [id]: url (title)
        // CommonMark spec: Link reference definition cannot have escaped brackets
        // Check for [id]: pattern, but id cannot be empty or just escaped characters
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith('['))
        {
            return false;
        }

        // Find the closing bracket
        var bracketEnd = trimmed.IndexOf(']', 1);
        if (bracketEnd < 0 || bracketEnd + 1 >= trimmed.Length || trimmed[bracketEnd + 1] != ':')
        {
            return false;
        }

        // Extract id - check for escaped brackets
        var id = trimmed.Substring(1, bracketEnd - 1);

        // If id contains only backslash (escaped bracket), it's not a valid link reference
        if (string.IsNullOrWhiteSpace(id) || id == "\\")
        {
            return false;
        }

        // Rest of line after ]: - may span multiple lines
        var rest = trimmed.Substring(bracketEnd + 2).TrimStart();
        var urlLines = new List<string> { rest };

        // CommonMark spec: Link reference definitions can span multiple lines
        // Continue reading lines until we find a blank line or non-indented line
        currentIndex++;
        while (currentIndex < this._lines!.Count)
        {
            var nextLine = this._lines[currentIndex];
            var nextTrimmed = nextLine.TrimStart();
            var nextIndent = nextLine.Length - nextTrimmed.Length;

            // Blank line ends the link reference definition
            if (string.IsNullOrWhiteSpace(nextTrimmed))
            {
                break;
            }

            // If line is indented, it's a continuation
            if (nextIndent > 0)
            {
                urlLines.Add(nextTrimmed);
                currentIndex++;
            }
            else
            {
                // Non-indented line ends the definition
                break;
            }
        }

        // Join all lines and parse URL and optional title
        var fullRest = string.Join(" ", urlLines).Trim();

        // Parse URL and optional title per CommonMark spec
        var url = fullRest;
        string? title = null;

        // Handle URL without quotes (CommonMark allows this)
        if (string.IsNullOrWhiteSpace(fullRest))
        {
            return false;
        }

        // Check for title in quotes or parentheses
        var firstChar = fullRest[0];
        if (firstChar == '"' || firstChar == '\'' || firstChar == '(')
        {
            var quoteChar = firstChar;
            var urlEnd = fullRest.IndexOf(quoteChar == '(' ? ')' : quoteChar, 1);
            if (urlEnd > 0)
            {
                url = fullRest.Substring(1, urlEnd - 1);
                if (fullRest.Length > urlEnd + 1)
                {
                    var titlePart = fullRest.Substring(urlEnd + 1).Trim();
                    if (titlePart.Length > 0 && (titlePart[0] == '"' || titlePart[0] == '\''))
                    {
                        var titleQuote = titlePart[0];
                        var titleEnd = titlePart.IndexOf(titleQuote, 1);
                        if (titleEnd > 0)
                        {
                            title = titlePart.Substring(1, titleEnd - 1);
                        }
                    }
                }
            }
        }
        else
        {
            // URL without quotes - find space separator for title
            var spaceIndex = fullRest.IndexOf(' ');
            if (spaceIndex > 0)
            {
                url = fullRest.Substring(0, spaceIndex);
                var titlePart = fullRest.Substring(spaceIndex + 1).Trim();
                if (titlePart.Length > 2 && (titlePart[0] == '"' || titlePart[0] == '\''))
                {
                    var quoteChar = titlePart[0];
                    var titleEnd = titlePart.IndexOf(quoteChar, 1);
                    if (titleEnd > 0)
                    {
                        title = titlePart.Substring(1, titleEnd - 1);
                    }
                }
            }
            else
            {
                // URL only, no title
                url = fullRest;
            }
        }

        // Normalize link reference id (collapse whitespace per CommonMark spec)
        var normalizedId = Regexes.WhitespaceCollapse.Replace(id, " ").Trim();

        definition = new LinkReferenceDefinition
        {
            Url = url.Trim(),
            Title = title
        };

        this._linkReferences![normalizedId] = definition;

        return true;
    }

    private bool TryParseHtmlBlock(string line, int lineIndex, ref int currentIndex, out HtmlBlockNode? htmlBlock)
    {
        htmlBlock = null;

        // CommonMark HTML blocks - 7 types per spec section 4.6-4.12
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith('<'))
        {
            return false;
        }

        var content = new StringBuilder();

        // Type 1: <script, <style, <pre, <iframe (followed by whitespace, >, or EOL)
        if (IsHtmlBlockType1Start(trimmed))
        {
            // Read until end tag found (reuse the cached HtmlBlock1Start regex which captures group 1)
            var tagMatch = Regexes.HtmlBlock1Start.Match(trimmed);
            var tagName = tagMatch.Groups[1].Value.ToLowerInvariant();
            var closingTag = $"</{tagName}>";

            while (currentIndex < this._lines!.Count)
            {
                var currentLine = this._lines[currentIndex];
                var closingIndex = currentLine.IndexOf(closingTag, StringComparison.OrdinalIgnoreCase);

                if (closingIndex >= 0)
                {
                    // Found closing tag - include only up to and including the closing tag
                    var tagEnd = closingIndex + closingTag.Length;
                    var lineContent = currentLine.Substring(0, tagEnd);
                    if (content.Length > 0)
                    {
                        _ = content.Append('\n');
                    }

                    _ = content.Append(lineContent);
                    currentIndex++;
                    break;
                }
                else
                {
                    // No closing tag yet - include entire line
                    if (content.Length > 0)
                    {
                        _ = content.Append('\n');
                    }

                    _ = content.Append(currentLine);
                    currentIndex++;
                }
            }

            htmlBlock = new HtmlBlockNode { Content = content.ToString(), Location = new SourceLocation(0, content.Length, lineIndex + 1, 1) };
            return true;
        }

        // Type 2: HTML comment <!-- ... -->
        if (trimmed.StartsWith("<!--"))
        {
            var startLine = this._lines![currentIndex];
            _ = content.Append(startLine);
            currentIndex++;

            // Check if closing --> is on the same line
            var closingIndex = startLine.IndexOf("-->");
            if (closingIndex >= 0)
            {
                // Closing is on the same line - extract only up to and including -->
                var commentEnd = closingIndex + 3;
                var commentContent = startLine.Substring(0, commentEnd);
                htmlBlock = new HtmlBlockNode { Content = commentContent, Location = new SourceLocation(0, commentContent.Length, lineIndex + 1, 1) };
                return true;
            }

            // Find closing --> on subsequent lines
            while (currentIndex < this._lines!.Count)
            {
                var currentLine = this._lines[currentIndex];
                var lineClosingIndex = currentLine.IndexOf("-->");

                if (lineClosingIndex >= 0)
                {
                    // Found closing --> on this line - include only up to and including -->
                    var commentEnd = lineClosingIndex + 3;
                    var lineContent = currentLine.Substring(0, commentEnd);
                    _ = content.Append('\n').Append(lineContent);
                    currentIndex++;
                    break;
                }
                else
                {
                    // No closing yet - include entire line
                    _ = content.Append('\n').Append(currentLine);
                    currentIndex++;
                }
            }

            htmlBlock = new HtmlBlockNode { Content = content.ToString(), Location = new SourceLocation(0, content.Length, lineIndex + 1, 1) };
            return true;
        }

        // Type 3: Processing instruction <? ... ?>
        if (trimmed.StartsWith("<?"))
        {
            var startLine = this._lines![currentIndex];
            _ = content.Append(startLine);
            currentIndex++;

            // Check if closing ?> is on the same line
            var closingIndex = startLine.IndexOf("?>");
            if (closingIndex >= 0)
            {
                // Closing is on the same line - extract only up to and including ?>
                var instructionEnd = closingIndex + 2;
                var instructionContent = startLine.Substring(0, instructionEnd);
                htmlBlock = new HtmlBlockNode { Content = instructionContent, Location = new SourceLocation(0, instructionContent.Length, lineIndex + 1, 1) };
                return true;
            }

            // Find closing ?> on subsequent lines
            while (currentIndex < this._lines!.Count)
            {
                var currentLine = this._lines[currentIndex];
                var lineClosingIndex = currentLine.IndexOf("?>");

                if (lineClosingIndex >= 0)
                {
                    // Found closing ?> on this line - include only up to and including ?>
                    var instructionEnd = lineClosingIndex + 2;
                    var lineContent = currentLine.Substring(0, instructionEnd);
                    _ = content.Append('\n').Append(lineContent);
                    currentIndex++;
                    break;
                }
                else
                {
                    // No closing yet - include entire line
                    _ = content.Append('\n').Append(currentLine);
                    currentIndex++;
                }
            }

            htmlBlock = new HtmlBlockNode { Content = content.ToString(), Location = new SourceLocation(0, content.Length, lineIndex + 1, 1) };
            return true;
        }

        // Type 4: Declaration <! followed by uppercase ASCII letter
        if (trimmed.StartsWith("<!") && trimmed.Length > 2 && char.IsUpper(trimmed[2]))
        {
            var startLine = this._lines![currentIndex];
            _ = content.Append(startLine);
            currentIndex++;

            // Check if closing > is on the same line
            var closingIndex = startLine.IndexOf('>');
            if (closingIndex >= 0)
            {
                // Closing is on the same line - extract only up to and including >
                var declarationEnd = closingIndex + 1;
                var declarationContent = startLine.Substring(0, declarationEnd);
                htmlBlock = new HtmlBlockNode { Content = declarationContent, Location = new SourceLocation(0, declarationContent.Length, lineIndex + 1, 1) };
                return true;
            }

            // Find closing > on subsequent lines
            while (currentIndex < this._lines!.Count)
            {
                var currentLine = this._lines[currentIndex];
                var lineClosingIndex = currentLine.IndexOf('>');

                if (lineClosingIndex >= 0)
                {
                    // Found closing > on this line - include only up to and including >
                    var declarationEnd = lineClosingIndex + 1;
                    var lineContent = currentLine.Substring(0, declarationEnd);
                    _ = content.Append('\n').Append(lineContent);
                    currentIndex++;
                    break;
                }
                else
                {
                    // No closing yet - include entire line
                    _ = content.Append('\n').Append(currentLine);
                    currentIndex++;
                }
            }

            htmlBlock = new HtmlBlockNode { Content = content.ToString(), Location = new SourceLocation(0, content.Length, lineIndex + 1, 1) };
            return true;
        }

        // Type 5: CDATA section <![CDATA[ ... ]]>
        if (trimmed.StartsWith("<![CDATA["))
        {
            var startLine = this._lines![currentIndex];
            _ = content.Append(startLine);
            currentIndex++;

            // Check if closing ]]> is on the same line
            var closingIndex = startLine.IndexOf("]]>");
            if (closingIndex >= 0)
            {
                // Closing is on the same line - extract only up to and including ]]>
                var cdataEnd = closingIndex + 3;
                var cdataContent = startLine.Substring(0, cdataEnd);
                htmlBlock = new HtmlBlockNode { Content = cdataContent, Location = new SourceLocation(0, cdataContent.Length, lineIndex + 1, 1) };
                return true;
            }

            // Find closing ]]> on subsequent lines
            while (currentIndex < this._lines!.Count)
            {
                var currentLine = this._lines[currentIndex];
                var lineClosingIndex = currentLine.IndexOf("]]>");

                if (lineClosingIndex >= 0)
                {
                    // Found closing ]]> on this line - include only up to and including ]]>
                    var cdataEnd = lineClosingIndex + 3;
                    var lineContent = currentLine.Substring(0, cdataEnd);
                    _ = content.Append('\n').Append(lineContent);
                    currentIndex++;
                    break;
                }
                else
                {
                    // No closing yet - include entire line
                    _ = content.Append('\n').Append(currentLine);
                    currentIndex++;
                }
            }

            htmlBlock = new HtmlBlockNode { Content = content.ToString(), Location = new SourceLocation(0, content.Length, lineIndex + 1, 1) };
            return true;
        }

        // Type 6: Start condition - opening tag or closing tag from predefined list
        if (IsHtmlBlockType6Start(trimmed))
        {
            _ = content.Append(this._lines![currentIndex]);
            currentIndex++;

            // Read until blank line
            while (currentIndex < this._lines!.Count)
            {
                var currentLine = this._lines[currentIndex];
                if (string.IsNullOrWhiteSpace(currentLine))
                {
                    break;
                }

                _ = content.Append('\n').Append(currentLine);
                currentIndex++;
            }

            htmlBlock = new HtmlBlockNode { Content = content.ToString(), Location = new SourceLocation(0, content.Length, lineIndex + 1, 1) };
            return true;
        }

        // Type 7: Complete open tag or closing tag (any tag except script, pre, style)
        if (IsHtmlBlockType7Start(trimmed))
        {
            _ = content.Append(this._lines![currentIndex]);
            currentIndex++;

            // Read until blank line
            while (currentIndex < this._lines!.Count)
            {
                var currentLine = this._lines[currentIndex];
                if (string.IsNullOrWhiteSpace(currentLine))
                {
                    break;
                }

                _ = content.Append('\n').Append(currentLine);
                currentIndex++;
            }

            htmlBlock = new HtmlBlockNode { Content = content.ToString(), Location = new SourceLocation(0, content.Length, lineIndex + 1, 1) };
            return true;
        }

        return false;
    }

    private static bool IsHtmlBlockType1Start(string line)
    {
        // <script, <style, <pre, <iframe followed by whitespace, >, or EOL
        return Regexes.HtmlBlock1Start.IsMatch(line);
    }

    private static bool IsHtmlBlockType6Start(string line)
    {
        // Opening or closing tag from predefined list, followed by whitespace, >, or EOL
        return Regexes.HtmlBlock6Start.IsMatch(line);
    }

    private static bool IsHtmlBlockType7Start(string line)
    {
        // Complete open tag or closing tag (except script, pre, style) or self-closing tag, followed by whitespace or EOL
        // Very permissive - matches any valid HTML tag-like structure
        return Regexes.HtmlBlock7Start.IsMatch(line) &&
               !Regexes.HtmlBlock7ExcludeStart.IsMatch(line);
    }

    private ParagraphNode? ParseParagraph(string line, int lineIndex, ref int currentIndex)
    {
        var contentLines = new List<string> { line };
        currentIndex++;

        // Continue paragraph until blank line or block construct
        while (currentIndex < this._lines!.Count)
        {
            var currentLine = this._lines[currentIndex];
            var trimmed = currentLine.TrimStart();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                // Blank line ends paragraph
                break;
            }

            // Check if next line starts a block construct
            if (this.IsBlockStart(trimmed))
            {
                break;
            }

            contentLines.Add(currentLine);
            currentIndex++;
        }

        var content = StringCompat.Join('\n', contentLines);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var paragraph = new ParagraphNode
        {
            Location = new SourceLocation(0, content.Length, lineIndex + 1, 1)
        };

        // Store raw text for Phase 2 inline parsing (preserve newlines for hard break detection)
        paragraph.Children.Add(new MarkdownTextNode { Content = content });

        return paragraph;
    }

    private bool IsBlockStart(string line)
    {
        // Check for fenced div: ::: followed by at least one non-space character (the attributes)
        if (line.Length >= 4 && line.StartsWith(":::"))
        {
            // Must have attributes after the colons to be an opening fence
            var afterColons = line.Substring(3).TrimStart();
            if (!string.IsNullOrWhiteSpace(afterColons))
            {
                return true;
            }
        }

        return line.StartsWith('#') ||
               line.StartsWith('>') ||
               (line.Length >= 3 && (line.StartsWith("---") || line.StartsWith("***") || line.StartsWith("___"))) ||
               line.StartsWith("```") ||
               line.StartsWith("~~~") ||
               Regexes.BulletListLine.IsMatch(line) ||
               Regexes.OrderedListLine.IsMatch(line) ||
               line.TrimStart().Length >= 4 && line.Substring(0, 4).All(c => c == ' ');
    }

    #endregion

    #region Phase 2: Inline Structure Parsing

    /// <summary>
    /// Phase 2: Parse inline structure within blocks
    /// </summary>
    private void ParseInlineStructure()
    {
        if (this._document == null)
        {
            return;
        }

        this.WalkTreeAndParseInlines(this._document);
    }

    private void WalkTreeAndParseInlines(Node node)
    {
        // Parse inlines in paragraphs, headings, and inline container nodes
        if (node is ParagraphNode paragraph)
        {
            this.ParseInlineContent(paragraph);
        }
        else if (node is HeadingNode heading)
        {
            this.ParseInlineContent(heading);
        }
        else if (node is LinkNode || node is EmphasisNode || node is StrongEmphasisNode || node is ImageNode)
        {
            // Parse inline content in link, emphasis, and image nodes for nested formatting
            this.ParseInlineContent((MarkdownContainerNode)node);
        }

        // Recursively process children.
        // ParseInlineContent above has already finished rebuilding this container's children list,
        // so we can iterate the list directly without a defensive copy.
        if (node is MarkdownContainerNode container)
        {
            var children = container.Children;
            for (var ci = 0; ci < children.Count; ci++)
            {
                this.WalkTreeAndParseInlines(children[ci]);
            }
        }
    }

    /// <summary>
    /// Phase 3: Apply smart punctuation transformations
    /// </summary>
    private void ApplySmartPunctuation()
    {
        if (this._document == null)
        {
            return;
        }

        this.WalkTreeAndApplySmartPunctuation(this._document);
    }

    private void WalkTreeAndApplySmartPunctuation(Node node, string? originalText = null)
    {
        // Get original text from stored map if this is a paragraph or heading
        if (node is MarkdownContainerNode container && this._originalTextMap!.TryGetValue(container, out var storedOriginalText))
        {
            originalText = storedOriginalText;
        }

        // Apply smart punctuation to text nodes (but not in code spans/blocks)
        if (node is MarkdownTextNode textNode)
        {
            textNode.Content = this.ApplySmartPunctuationToText(textNode.Content, originalText);
        }

        // Recursively process children.
        // Smart punctuation only mutates text node Content, not the children collection,
        // so we can iterate directly without a defensive copy.
        if (node is MarkdownContainerNode containerNode)
        {
            var children = containerNode.Children;
            for (var ci = 0; ci < children.Count; ci++)
            {
                this.WalkTreeAndApplySmartPunctuation(children[ci], originalText);
            }
        }
    }

    private void ParseInlineContent(MarkdownContainerNode container)
    {
        // Store original text before inline parsing for smart punctuation.
        // We only need the first raw text node's content (the whole paragraph/heading text).
        string? originalText = null;
        if (container is ParagraphNode || container is HeadingNode)
        {
            var children = container.Children;
            for (var ci = 0; ci < children.Count; ci++)
            {
                if (children[ci] is MarkdownTextNode firstText)
                {
                    originalText = firstText.Content;
                    if (this._originalTextMap != null)
                    {
                        this._originalTextMap[container] = originalText;
                    }

                    break;
                }
            }
        }

        // Single-pass rebuild: expand every MarkdownTextNode into its parsed inline nodes
        // in-place by building a new list. This avoids O(n) IndexOf + O(n) Insert per node.
        var old = container.Children;
        var hasAnyText = false;
        for (var ci = 0; ci < old.Count; ci++)
        {
            if (old[ci] is MarkdownTextNode)
            {
                hasAnyText = true;
                break;
            }
        }

        if (!hasAnyText)
        {
            return;
        }

        var rebuilt = new List<Node>(old.Count * 2);
        for (var ci = 0; ci < old.Count; ci++)
        {
            var child = old[ci];
            if (child is MarkdownTextNode textNode)
            {
                var newNodes = this.ParseInlineText(textNode.Content, textNode.Location.Offset, originalText ?? textNode.Content);
                foreach (var newNode in newNodes)
                {
                    newNode.SetParent(container);
                    rebuilt.Add(newNode);
                }
            }
            else
            {
                rebuilt.Add(child);
            }
        }

        container.Children.Clear();
        container.Children.AddRange(rebuilt);
    }

    private List<Node> ParseInlineText(string text, int baseOffset, string originalText)
    {
        var result = new List<Node>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        var i = 0;
        var currentText = new StringBuilder();

        while (i < text.Length)
        {
            // Code span: `code` or ``code with `backticks` ``
            if (text[i] == '`')
            {
                // Flush current text
                if (currentText.Length > 0)
                {
                    result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                    _ = currentText.Clear();
                }

                // Count consecutive backticks
                var backtickCount = 1;
                while (i + backtickCount < text.Length && text[i + backtickCount] == '`')
                {
                    backtickCount++;
                }

                // Find matching closing backticks
                var codeStart = i + backtickCount;
                var searchStart = codeStart;
                var codeEnd = -1;

                while (searchStart < text.Length)
                {
                    var foundPos = text.IndexOf('`', searchStart);
                    if (foundPos < 0)
                    {
                        break;
                    }

                    // Count consecutive backticks at this position
                    var closingCount = 1;
                    while (foundPos + closingCount < text.Length && text[foundPos + closingCount] == '`')
                    {
                        closingCount++;
                    }

                    if (closingCount == backtickCount)
                    {
                        codeEnd = foundPos;
                        break;
                    }

                    searchStart = foundPos + closingCount;
                }

                if (codeEnd > 0)
                {
                    var codeContent = text.Substring(codeStart, codeEnd - codeStart);
                    result.Add(new CodeSpanNode { Content = codeContent });
                    i = codeEnd + backtickCount;
                    continue;
                }
            }

            // Emphasis: *text* or _text* - using CommonMark full delimiter stack algorithm
            if (text[i] == '*' || text[i] == '_')
            {
                // Try to parse emphasis first WITHOUT flushing
                var consumed = this.TryParseEmphasisCommonMark(text, i, result, baseOffset, originalText);
                if (consumed > 0)
                {
                    // Emphasis was successfully parsed
                    // Flush current text BEFORE the emphasis node by inserting at the correct position
                    if (currentText.Length > 0)
                    {
                        var textNode = new MarkdownTextNode { Content = currentText.ToString() };
                        // Insert before the just-added emphasis node
                        result.Insert(result.Count - 1, textNode);
                        _ = currentText.Clear();
                    }

                    i += consumed;
                    continue;
                }
                // If consumed == 0, fall through to append as literal character
            }

            // Autolinks: <http://example.com> or <user@example.com>
            if (text[i] == '<')
            {
                var closeIdx = text.IndexOf('>', i + 1);
                if (closeIdx > i + 1)
                {
                    var content = text.Substring(i + 1, closeIdx - i - 1);

                    // Check if it's a URL autolink
                    if (content.Contains("://") && (content.StartsWith("http://") || content.StartsWith("https://") || content.StartsWith("ftp://")))
                    {
                        // Flush current text
                        if (currentText.Length > 0)
                        {
                            result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                            _ = currentText.Clear();
                        }

                        // Create autolink
                        var autolinkNode = new LinkNode { Url = content };
                        autolinkNode.Children.Add(new MarkdownTextNode { Content = content });
                        result.Add(autolinkNode);
                        i = closeIdx + 1;
                        continue;
                    }

                    // Check if it's an email autolink
                    if (content.Contains('@') && !content.StartsWith('@') && !content.EndsWith('@'))
                    {
                        var atIdx = content.IndexOf('@');
                        var beforeAt = content.AsSpan(0, atIdx);
                        var afterAt = content.AsSpan(atIdx + 1);

                        // Simple email validation: non-empty before and after @, with domain having a dot
                        if (beforeAt.Length > 0 && afterAt.Length > 0 && afterAt.Contains('.'))
                        {
                            // Flush current text
                            if (currentText.Length > 0)
                            {
                                result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                                _ = currentText.Clear();
                            }

                            // Create email autolink
                            var emailLink = new LinkNode { Url = "mailto:" + content };
                            emailLink.Children.Add(new MarkdownTextNode { Content = content });
                            result.Add(emailLink);
                            i = closeIdx + 1;
                            continue;
                        }
                    }

                    // Check if it's raw HTML inline: <span>, <div>, etc.
                    if (content.Length > 0 && (char.IsLetter(content[0]) || content[0] == '/' || content[0] == '!'))
                    {
                        // Simple HTML tag check: starts with letter or / or !
                        var firstWord = content.Split(' ', '>', '/')[0];
                        if (firstWord.Length > 0 && (char.IsLetter(firstWord[0]) || firstWord[0] == '/' || firstWord[0] == '!'))
                        {
                            // This looks like an HTML tag - preserve as raw HTML
                            // Flush current text
                            if (currentText.Length > 0)
                            {
                                result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                                _ = currentText.Clear();
                            }

                            // Create raw HTML node by reusing the paragraph content as-is
                            // Since we don't have a specific RawHtmlNode, we'll create a special text node
                            result.Add(new MarkdownTextNode { Content = text.Substring(i, closeIdx - i + 1) });
                            i = closeIdx + 1;
                            continue;
                        }
                    }
                }
            }

            // HTML Entities: &amp; &#123; &#x1F600; etc.
            if (text[i] == '&')
            {
                var semiIdx = text.IndexOf(';', i + 1);
                if (semiIdx > i + 1 && semiIdx - i <= 32)  // Reasonable max length for entity
                {
                    var entityContent = text.Substring(i + 1, semiIdx - i - 1);

                    // Named entity: &name;
                    if (entityContent.Length > 0 && char.IsLetter(entityContent[0]))
                    {
                        // Check if it's a valid named entity
                        var entityValue = this.ResolveNamedEntity(entityContent);
                        if (entityValue != null)
                        {
                            // Flush current text
                            if (currentText.Length > 0)
                            {
                                result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                                _ = currentText.Clear();
                            }

                            result.Add(new MarkdownTextNode { Content = entityValue });
                            i = semiIdx + 1;
                            continue;
                        }
                    }

                    // Decimal entity: &#123;
                    if (entityContent.StartsWith('#') && entityContent.Length > 1 && char.IsDigit(entityContent[1]))
                    {
                        if (Int32Compat.TryParse(entityContent.AsSpan(1), out var codePoint) && codePoint >= 0 && codePoint <= 0x10FFFF)
                        {
                            // Flush current text
                            if (currentText.Length > 0)
                            {
                                result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                                _ = currentText.Clear();
                            }

                            try
                            {
                                var entityChar = char.ConvertFromUtf32(codePoint);
                                result.Add(new MarkdownTextNode { Content = entityChar });
                                i = semiIdx + 1;
                                continue;
                            }
                            catch
                            {
                                // Invalid code point
                            }
                        }
                    }

                    // Hex entity: &#x1F600;
                    if (entityContent.StartsWith("#x", StringComparison.OrdinalIgnoreCase) && entityContent.Length > 2)
                    {
                        if (Int32Compat.TryParse(entityContent.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hexCodePoint) && hexCodePoint >= 0 && hexCodePoint <= 0x10FFFF)
                        {
                            // Flush current text
                            if (currentText.Length > 0)
                            {
                                result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                                _ = currentText.Clear();
                            }

                            try
                            {
                                var entityChar = char.ConvertFromUtf32(hexCodePoint);
                                result.Add(new MarkdownTextNode { Content = entityChar });
                                i = semiIdx + 1;
                                continue;
                            }
                            catch
                            {
                                // Invalid code point
                            }
                        }
                    }
                }
            }

            // Link: [text](url) or [text][ref] - check BEFORE images since links can contain images
            if (text[i] == '[')
            {
                // Flush current text
                if (currentText.Length > 0)
                {
                    result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                    _ = currentText.Clear();
                }

                // Find matching closing bracket, handling nested brackets (e.g., [![alt](img.png)])
                var linkEnd = -1;
                var bracketDepth = 1;
                for (var j = i + 1; j < text.Length; j++)
                {
                    if (text[j] == '[')
                    {
                        bracketDepth++;
                    }
                    else if (text[j] == ']')
                    {
                        bracketDepth--;
                        if (bracketDepth == 0)
                        {
                            linkEnd = j;
                            break;
                        }
                    }
                }

                if (linkEnd > 0)
                {
                    var linkText = text.Substring(i + 1, linkEnd - i - 1);

                    // Check for inline link: (url) or (url "title")
                    if (linkEnd + 1 < text.Length && text[linkEnd + 1] == '(')
                    {
                        var urlStart = linkEnd + 2;
                        var urlEnd = text.IndexOf(')', urlStart);
                        if (urlEnd > 0)
                        {
                            var urlAndTitle = text.Substring(urlStart, urlEnd - urlStart).Trim();
                            var url = urlAndTitle;
                            string? title = null;

                            // Check for title in quotes (handle escaped quotes)
                            var spaceIndex = urlAndTitle.IndexOf(' ');
                            if (spaceIndex > 0 && spaceIndex < urlAndTitle.Length - 1)
                            {
                                var titlePart = urlAndTitle.Substring(spaceIndex + 1).Trim();
                                if (titlePart.Length > 2 && (titlePart[0] == '"' || titlePart[0] == '\''))
                                {
                                    var quoteChar = titlePart[0];
                                    // Find closing quote, handling escaped quotes
                                    var titleEnd = -1;
                                    for (var j = 1; j < titlePart.Length; j++)
                                    {
                                        if (titlePart[j] == quoteChar && (j == 0 || titlePart[j - 1] != '\\'))
                                        {
                                            titleEnd = j;
                                            break;
                                        }
                                    }

                                    if (titleEnd > 0)
                                    {
                                        url = urlAndTitle.Substring(0, spaceIndex).Trim();
                                        title = titlePart.Substring(1, titleEnd - 1).Replace("\\\"", "\"").Replace("\\'", "'");
                                    }
                                }
                            }

                            var link = new LinkNode { Url = url, Title = title };

                            // Parse link text for inline elements (images, emphasis, etc.)
                            var linkTextNodes = this.ParseInlineText(linkText, baseOffset + i + 1, linkText);
                            foreach (var node in linkTextNodes)
                            {
                                link.Children.Add(node);
                            }

                            result.Add(link);
                            i = urlEnd + 1;
                            continue;
                        }
                    }

                    // Check for reference link: [ref] or shortcut reference [text]
                    // First try explicit reference [text][ref]
                    if (linkEnd + 1 < text.Length && text[linkEnd + 1] == '[')
                    {
                        var refEnd = text.IndexOf(']', linkEnd + 2);
                        if (refEnd > 0)
                        {
                            var refId = text.Substring(linkEnd + 2, refEnd - linkEnd - 2);
                            // Normalize reference id (collapse whitespace)
                            var normalizedRefId = Regexes.WhitespaceCollapse.Replace(refId, " ").Trim();
                            if (this._linkReferences!.TryGetValue(normalizedRefId, out var linkRef))
                            {
                                var link = new LinkNode { Url = linkRef.Url, Title = linkRef.Title };
                                link.Children.Add(new MarkdownTextNode { Content = linkText });
                                result.Add(link);
                                i = refEnd + 1;
                                continue;
                            }
                        }
                    }
                    else
                    {
                        // Shortcut reference: [text] where text matches a link reference id
                        // Normalize link text (collapse whitespace)
                        var normalizedLinkText = Regexes.WhitespaceCollapse.Replace(linkText, " ").Trim();
                        if (this._linkReferences!.TryGetValue(normalizedLinkText, out var shortcutRef))
                        {
                            var link = new LinkNode { Url = shortcutRef.Url, Title = shortcutRef.Title };
                            link.Children.Add(new MarkdownTextNode { Content = linkText });
                            result.Add(link);
                            i = linkEnd + 1;
                            continue;
                        }
                    }
                }
            }


            // Hard line break: backslash followed by newline, or two spaces before newline
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                // Backslash followed by newline = hard line break
                // Flush current text
                if (currentText.Length > 0)
                {
                    result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                    _ = currentText.Clear();
                }

                result.Add(new HardLineBreakNode());
                i += 2; // Skip backslash and newline
                continue;
            }

            // Two spaces before newline = hard line break
            if (i + 2 < text.Length && text[i] == ' ' && text[i + 1] == ' ' && text[i + 2] == '\n')
            {
                // Flush current text
                if (currentText.Length > 0)
                {
                    result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                    _ = currentText.Clear();
                }

                result.Add(new HardLineBreakNode());
                i += 3; // Skip two spaces and newline
                continue;
            }

            // Single newline = soft line break
            if (text[i] == '\n')
            {
                // Flush current text
                if (currentText.Length > 0)
                {
                    result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                    _ = currentText.Clear();
                }

                result.Add(new SoftLineBreakNode());
                i++;
                continue;
            }

            // Image: ![alt](url) - parse AFTER links so links can contain images
            if (i + 1 < text.Length && text[i] == '!' && text[i + 1] == '[')
            {
                // Flush current text
                if (currentText.Length > 0)
                {
                    result.Add(new MarkdownTextNode { Content = currentText.ToString() });
                    _ = currentText.Clear();
                }

                var altEnd = text.IndexOf(']', i + 2);
                if (altEnd > 0 && altEnd + 1 < text.Length && text[altEnd + 1] == '(')
                {
                    var altText = text.Substring(i + 2, altEnd - i - 2);
                    var urlStart = altEnd + 2;
                    var urlEnd = text.IndexOf(')', urlStart);
                    if (urlEnd > 0)
                    {
                        var urlAndTitle = text.Substring(urlStart, urlEnd - urlStart).Trim();
                        var url = urlAndTitle;
                        string? title = null;

                        // Check for title in quotes (handle escaped quotes)
                        var spaceIndex = urlAndTitle.IndexOf(' ');
                        if (spaceIndex > 0 && spaceIndex < urlAndTitle.Length - 1)
                        {
                            var titlePart = urlAndTitle.Substring(spaceIndex + 1).Trim();
                            if (titlePart.Length > 2 && (titlePart[0] == '"' || titlePart[0] == '\''))
                            {
                                var quoteChar = titlePart[0];
                                // Find closing quote, handling escaped quotes
                                var titleEnd = -1;
                                for (var j = 1; j < titlePart.Length; j++)
                                {
                                    if (titlePart[j] == quoteChar && (j == 0 || titlePart[j - 1] != '\\'))
                                    {
                                        titleEnd = j;
                                        break;
                                    }
                                }

                                if (titleEnd > 0)
                                {
                                    url = urlAndTitle.Substring(0, spaceIndex).Trim();
                                    title = titlePart.Substring(1, titleEnd - 1).Replace("\\\"", "\"").Replace("\\'", "'");
                                }
                            }
                        }

                        var image = new ImageNode { Url = url, Title = title };
                        image.Children.Add(new MarkdownTextNode { Content = altText });
                        result.Add(image);
                        i = urlEnd + 1;
                        continue;
                    }
                }
            }

            // Handle escaped characters - process them but track for smart punctuation
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                var nextCh = text[i + 1];
                // Check if this is an escaped punctuation that affects smart punctuation
                if (nextCh == '-' || nextCh == '.')
                {
                    // Track escaped sequences for smart punctuation
                    // For now, just append the character (backslash removed per CommonMark)
                    _ = currentText.Append(nextCh);
                    i += 2;
                    continue;
                }
                else if (nextCh == '"' || nextCh == '\'')
                {
                    // Escaped quotes - append character (backslash removed)
                    _ = currentText.Append(nextCh);
                    i += 2;
                    continue;
                }
                // Other escaped characters handled elsewhere
            }

            _ = currentText.Append(text[i]);
            i++;
        }

        // Flush remaining text
        if (currentText.Length > 0)
        {
            var textContent = currentText.ToString();
            // Store original text with escaped sequences marked for smart punctuation
            var textNode = new MarkdownTextNode { Content = textContent };
            // We'll track escaped sequences by checking originalText during smart punctuation
            result.Add(textNode);
        }

        return result;
    }

    /// <summary>
    /// Try to parse emphasis (*, _, emphasis/strong) at current position using CommonMark delimiter stack.
    /// For now, this is a simple implementation. A more sophisticated approach would use the 
    /// CommonMarkDelimiterProcessor to handle complex nesting.
    /// </summary>
    /// <returns>Number of characters consumed if emphasis was parsed, 0 if not emphasis.</returns>
    private int TryParseEmphasisCommonMark(string text, int startPos, List<Node> result, int baseOffset, string originalText)
    {
        if (startPos >= text.Length)
        {
            return 0;
        }

        var marker = text[startPos];
        if (marker != '*' && marker != '_')
        {
            return 0;
        }

        // For triple markers like ***, we prefer to parse as * ** or ** *
        // But for simplicity, we'll check for the most specific match first

        // Try matching 2 markers (strong) first if we have at least 2
        var markerCount = 1;
        while (startPos + markerCount < text.Length && text[startPos + markerCount] == marker)
        {
            markerCount++;
        }

        // Try to find a matching closer
        // Strategy: prefer exact matches or slightly larger, allow 1 less if necessary
        for (var tryUseCount = Math.Min(markerCount, 2); tryUseCount >= 1; tryUseCount--)
        {
            var searchStart = startPos + markerCount;  // Skip ALL opening markers
            var closerPos = -1;

            // Search for closing marker with same count or more
            while (searchStart < text.Length)
            {
                if (text[searchStart] == marker)
                {
                    var closingCount = 1;
                    while (searchStart + closingCount < text.Length && text[searchStart + closingCount] == marker)
                    {
                        closingCount++;
                    }

                    // Accept if we have at least tryUseCount closing markers
                    if (closingCount >= tryUseCount)
                    {
                        closerPos = searchStart;
                        break;
                    }

                    searchStart += closingCount;
                }
                else
                {
                    searchStart++;
                }
            }

            if (closerPos > startPos + markerCount)
            {
                // Found a match!
                var contentStart = startPos + markerCount;
                var contentEnd = closerPos;

                // Count closing markers to determine exact count
                var closingCount = 1;
                while (closerPos + closingCount < text.Length && text[closerPos + closingCount] == marker)
                {
                    closingCount++;
                }

                // Include unconsumed opening markers in content (for nested emphasis)
                var unconsumedOpeningMarkers = markerCount - tryUseCount;
                var unconsumedClosingMarkers = closingCount - tryUseCount;

                // Rebuild the content with unconsumed markers
                var contentBuilder = new StringBuilder();
                for (var i = 0; i < unconsumedOpeningMarkers; i++)
                {
                    contentBuilder.Append(marker);
                }

                contentBuilder.Append(text.AsSpan(contentStart, contentEnd - contentStart));
                for (var i = 0; i < unconsumedClosingMarkers; i++)
                {
                    contentBuilder.Append(marker);
                }

                var content = contentBuilder.ToString();

                // Recursively parse content for nested emphasis/inline elements
                var innerContent = this.ParseInlineText(content, baseOffset + contentStart, content);

                Node emphasisNode;
                if (tryUseCount == 2)
                {
                    var node = new StrongEmphasisNode();
                    node.Children.AddRange(innerContent);
                    // Set parent for all children
                    foreach (var child in innerContent)
                    {
                        child.SetParent(node);
                    }

                    emphasisNode = node;
                }
                else
                {
                    var node = new EmphasisNode();
                    node.Children.AddRange(innerContent);
                    // Set parent for all children
                    foreach (var child in innerContent)
                    {
                        child.SetParent(node);
                    }

                    emphasisNode = node;
                }

                result.Add(emphasisNode);

                // Return total characters consumed (all opening + content + all closing markers)
                return markerCount + (closerPos - contentStart) + closingCount;
            }
        }

        // No match found
        return 0;
    }

    /// <summary>
    /// Apply smart punctuation transformations to text.
    /// Transforms straight quotes to curly quotes, hyphens to dashes, and periods to ellipses.
    /// Uses a delimiter stack approach similar to emphasis parsing for quote matching.
    /// </summary>
    /// <param name="text">The text to transform (after inline parsing)</param>
    /// <param name="originalText">Optional original text (before inline parsing) to check for escaped characters</param>
    private string ApplySmartPunctuationToText(string text, string? originalText)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);

        // Build set of escaped sequences from original text
        // Track sequences that were escaped (without backslashes, as they appear after inline parsing)
        var escapedSequences = new HashSet<string>();
        if (!string.IsNullOrEmpty(originalText))
        {
            var origI = 0;
            while (origI < originalText!.Length)
            {
                if (originalText[origI] == '\\' && origI + 1 < originalText.Length)
                {
                    var nextCh = originalText[origI + 1];
                    if (nextCh == '-')
                    {
                        // Check for escaped dash sequences
                        // Could be \-- (two dashes) or multiple \-\- (consecutive escaped dashes)
                        var dashCount = 1;
                        var checkPos = origI + 2;

                        // First, count dashes after this backslash
                        while (checkPos < originalText.Length && originalText[checkPos] == '-')
                        {
                            dashCount++;
                            checkPos++;
                        }

                        // Then check for consecutive escaped dashes (e.g., \-\-\-)
                        while (checkPos < originalText.Length &&
                               originalText[checkPos] == '\\' &&
                               checkPos + 1 < originalText.Length &&
                               originalText[checkPos + 1] == '-')
                        {
                            dashCount++;
                            checkPos += 2; // Skip \-
                            // Count any additional dashes after this backslash
                            while (checkPos < originalText.Length && originalText[checkPos] == '-')
                            {
                                dashCount++;
                                checkPos++;
                            }
                        }

                        // Store the sequence without backslash (as it appears after inline parsing)
                        _ = escapedSequences.Add(new string('-', dashCount));
                        origI = checkPos;
                        continue;
                    }
                    else if (nextCh == '.')
                    {
                        // Check for escaped period sequences
                        var periodCount = 1;
                        var checkPos = origI + 2;

                        // Count periods after this backslash
                        while (checkPos < originalText.Length && originalText[checkPos] == '.')
                        {
                            periodCount++;
                            checkPos++;
                        }

                        // Check for consecutive escaped periods (e.g., \.\.\.)
                        while (checkPos < originalText.Length &&
                               originalText[checkPos] == '\\' &&
                               checkPos + 1 < originalText.Length &&
                               originalText[checkPos + 1] == '.')
                        {
                            periodCount++;
                            checkPos += 2; // Skip \.
                            // Count any additional periods after this backslash
                            while (checkPos < originalText.Length && originalText[checkPos] == '.')
                            {
                                periodCount++;
                                checkPos++;
                            }
                        }

                        if (periodCount >= 3)
                        {
                            _ = escapedSequences.Add(new string('.', periodCount));
                        }

                        origI = checkPos;
                        continue;
                    }
                    else if (nextCh == '"' || nextCh == '\'')
                    {
                        // Escaped quote - mark it (we'll check if any quotes were escaped)
                        _ = escapedSequences.Add(nextCh.ToString());
                        origI += 2;
                        continue;
                    }
                }

                origI++;
            }
        }

        var escapedPositions = new HashSet<int>(); // Track escaped character positions in processed text
        var quoteTypes = new List<(int pos, char type)>(); // 'd' for double, 's' for single

        // First pass: identify escaped characters and build quote list
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                var nextCh = text[i + 1];
                if (nextCh == '"' || nextCh == '\'' || nextCh == '-' || nextCh == '.')
                {
                    _ = escapedPositions.Add(i + 1);
                    i += 2;
                    continue;
                }
            }
            else if (text[i] == '"' && !escapedPositions.Contains(i))
            {
                quoteTypes.Add((i, 'd'));
            }
            else if (text[i] == '\'' && !escapedPositions.Contains(i))
            {
                quoteTypes.Add((i, 's'));
            }

            i++;
        }

        // Match quotes using delimiter stack algorithm.
        // openingQuotePositions: opening pos -> closing pos  (O(1) lookup for opening quotes)
        // closingQuotePositions: closing pos -> opening pos  (O(1) lookup — replaces ContainsValue)
        var openingQuotePositions = new Dictionary<int, int>();
        var closingQuotePositions = new Dictionary<int, int>();
        var openingQuotes = new Stack<(int pos, char type)>();

        foreach (var (pos, type) in quoteTypes)
        {
            // Check if single quote is an apostrophe (between letters/digits)
            if (type == 's')
            {
                var prevIsLetter = pos > 0 && char.IsLetterOrDigit(text[pos - 1]);
                var nextIsLetter = pos + 1 < text.Length && (char.IsLetterOrDigit(text[pos + 1]) || text[pos + 1] == 's');
                if (prevIsLetter && nextIsLetter)
                {
                    // This is an apostrophe, skip it
                    continue;
                }
            }

            if (openingQuotes.Count > 0 && openingQuotes.Peek().type == type)
            {
                // Match with opening quote
                var opening = openingQuotes.Pop();
                openingQuotePositions[opening.pos] = pos;
                closingQuotePositions[pos] = opening.pos;
            }
            else
            {
                // Opening quote
                openingQuotes.Push((pos, type));
            }
        }

        // Second pass: apply transformations
        i = 0;
        while (i < text.Length)
        {
            var ch = text[i];

            // Handle escaped characters - output backslash and character literally
            if (i > 0 && text[i - 1] == '\\' && escapedPositions.Contains(i))
            {
                // This character was escaped, output it literally (backslash already output)
                _ = result.Append(ch);
                i++;
                continue;
            }
            else if (ch == '\\' && i + 1 < text.Length && escapedPositions.Contains(i + 1))
            {
                // Output backslash and escaped character literally
                _ = result.Append(ch);
                _ = result.Append(text[i + 1]);
                i += 2;
                continue;
            }

            // Smart quotes - double quotes
            if (ch == '"')
            {
                // Check if this specific quote was escaped in original text
                // We can't easily map positions, so check if any quotes were escaped
                // This is a simplification - ideally we'd track positions
                var quoteWasEscaped = !string.IsNullOrEmpty(originalText) &&
                    originalText!.Contains("\\\"");
                if (quoteWasEscaped)
                {
                    // Escaped - keep as straight quote (but only first time)
                    _ = result.Append('"');
                    i++;
                    continue;
                }

                if (openingQuotePositions.ContainsKey(i))
                {
                    // Opening quote
                    _ = result.Append('\u201C'); // Left double quotation mark
                }
                else if (closingQuotePositions.ContainsKey(i))
                {
                    // Closing quote
                    _ = result.Append('\u201D'); // Right double quotation mark
                }
                else
                {
                    // Unmatched quote - treat as opening
                    _ = result.Append('\u201C');
                }

                i++;
                continue;
            }

            // Smart quotes - single quotes
            if (ch == '\'')
            {
                // Check if this specific quote was escaped in original text
                var quoteWasEscaped = !string.IsNullOrEmpty(originalText) &&
                    originalText!.Contains("\\'");
                if (quoteWasEscaped)
                {
                    // Escaped - keep as straight quote
                    _ = result.Append('\'');
                    i++;
                    continue;
                }

                // Check if apostrophe
                var prevIsLetter = i > 0 && char.IsLetterOrDigit(text[i - 1]);
                var nextIsLetter = i + 1 < text.Length && (char.IsLetterOrDigit(text[i + 1]) || text[i + 1] == 's');
                if (prevIsLetter && nextIsLetter)
                {
                    // Apostrophe
                    _ = result.Append('\u2019'); // Right single quotation mark (apostrophe)
                }
                else if (openingQuotePositions.ContainsKey(i))
                {
                    // Opening quote
                    _ = result.Append('\u2018'); // Left single quotation mark
                }
                else if (closingQuotePositions.ContainsKey(i))
                {
                    // Closing quote
                    _ = result.Append('\u2019'); // Right single quotation mark
                }
                else
                {
                    // Unmatched quote - treat as opening
                    _ = result.Append('\u2018');
                }

                i++;
                continue;
            }

            // Ellipses: three periods
            if (ch == '.' && i + 2 < text.Length && text[i + 1] == '.' && text[i + 2] == '.')
            {
                // Check if this sequence was escaped in original text
                var periodSequence = "...";
                if (!escapedSequences.Contains(periodSequence))
                {
                    _ = result.Append('\u2026'); // Ellipsis
                    i += 3;
                }
                else
                {
                    // Escaped - keep as periods
                    _ = result.Append(periodSequence);
                    i += 3;
                }

                continue;
            }

            // Dashes: need to handle sequences
            if (ch == '-')
            {
                var dashCount = 1;
                while (i + dashCount < text.Length && text[i + dashCount] == '-')
                {
                    dashCount++;
                }

                var dashSequence = new string('-', dashCount);

                // Check if this sequence was escaped in original text
                if (escapedSequences.Contains(dashSequence))
                {
                    // Escaped - keep as hyphens
                    _ = result.Append(dashSequence);
                    i += dashCount;
                    continue;
                }

                // Transform dashes
                if (dashCount == 2)
                {
                    // En-dash
                    _ = result.Append('\u2013');
                    i += 2;
                }
                else if (dashCount == 3)
                {
                    // Em-dash
                    _ = result.Append('\u2014');
                    i += 3;
                }
                else if (dashCount > 3)
                {
                    // Multiple dashes: convert to em-dashes and en-dashes
                    // Algorithm: prefer homogeneous sequences, em-dashes first
                    var remaining = dashCount;

                    // Use as many em-dashes as possible (3 hyphens each)
                    var emDashes = remaining / 3;
                    remaining %= 3;

                    // Use en-dashes for remainder (2 hyphens each)
                    var enDashes = remaining / 2;
                    remaining %= 2;

                    // If we have 1 remaining, convert last em-dash to en-dash + en-dash
                    if (remaining == 1 && emDashes > 0)
                    {
                        emDashes--;
                        enDashes += 2;
                    }

                    // Output em-dashes first
                    for (var j = 0; j < emDashes; j++)
                    {
                        _ = result.Append('\u2014');
                    }

                    // Then en-dashes
                    for (var j = 0; j < enDashes; j++)
                    {
                        _ = result.Append('\u2013');
                    }

                    i += dashCount;
                }
                else
                {
                    // Single dash - keep as is
                    _ = result.Append(ch);
                    i++;
                }

                continue;
            }

            // Regular character
            _ = result.Append(ch);
            i++;
        }

        return result.ToString();
    }

    private int FindEmphasisCloser(string text, int startPos, char marker, int markerCount)
    {
        var i = startPos;
        while (i < text.Length)
        {
            if (text[i] == marker)
            {
                var count = 1;
                while (i + count < text.Length && text[i + count] == marker)
                {
                    count++;
                }

                if (count == markerCount)
                {
                    // Check if it's a valid closer (not followed by alphanumeric)
                    if (i + count >= text.Length || !char.IsLetterOrDigit(text[i + count]))
                    {
                        return i;
                    }
                }
            }

            i++;
        }

        return -1;
    }

    /// <summary>
    /// Finds the best matching emphasis closer with support for proper nesting.
    /// This is part of the improved delimiter stack algorithm.
    /// </summary>
    private int FindBestEmphasisCloser(string text, int openerPos, char marker, int openerCount)
    {
        var searchStart = openerPos + openerCount;
        var bestCloser = -1;

        // Scan for potential closers - prefer exact match first, then fall back to partial
        for (var i = searchStart; i < text.Length; i++)
        {
            if (text[i] == marker)
            {
                var closerCount = 1;
                while (i + closerCount < text.Length && text[i + closerCount] == marker)
                {
                    closerCount++;
                }

                // Look for closest matching closer
                // Prefer exact count match, otherwise use any closer with sufficient delimiters
                if (closerCount >= openerCount)
                {
                    bestCloser = i;
                    break; // Take first valid closer
                }
                else if (closerCount > 0 && bestCloser < 0)
                {
                    bestCloser = i; // Keep searching for exact match
                }

                i += closerCount - 1; // Skip past this delimiter sequence
            }
        }

        return bestCloser;
    }

    /// <summary>
    /// Count trailing delimiter characters at a given position.
    /// </summary>
    private int CountTrailingDelimiters(string text, int pos, char delimiter)
    {
        if (pos < 0 || pos >= text.Length || text[pos] != delimiter)
        {
            return 0;
        }

        var count = 1;
        while (pos + count < text.Length && text[pos + count] == delimiter)
        {
            count++;
        }

        return count;
    }

    private string? ResolveNamedEntity(string entityName)
    {
        // Common HTML entities per CommonMark spec
        return entityName switch
        {
            "quot" => "\"",
            "amp" => "&",
            "lt" => "<",
            "gt" => ">",
            "apos" => "'",
            "nbsp" => "\u00A0",
            "iexcl" => "\u00A1",
            "cent" => "\u00A2",
            "pound" => "\u00A3",
            "curren" => "\u00A4",
            "yen" => "\u00A5",
            "brvbar" => "\u00A6",
            "sect" => "\u00A7",
            "uml" => "\u00A8",
            "copy" => "\u00A9",
            "ordf" => "\u00AA",
            "laquo" => "\u00AB",
            "not" => "\u00AC",
            "shy" => "\u00AD",
            "reg" => "\u00AE",
            "macr" => "\u00AF",
            "deg" => "\u00B0",
            "plusmn" => "\u00B1",
            "sup2" => "\u00B2",
            "sup3" => "\u00B3",
            "acute" => "\u00B4",
            "micro" => "\u00B5",
            "para" => "\u00B6",
            "middot" => "\u00B7",
            "cedil" => "\u00B8",
            "sup1" => "\u00B9",
            "ordm" => "\u00BA",
            "raquo" => "\u00BB",
            "frac14" => "\u00BC",
            "frac12" => "\u00BD",
            "frac34" => "\u00BE",
            "iquest" => "\u00BF",
            "Agrave" => "\u00C0",
            "Aacute" => "\u00C1",
            "Acirc" => "\u00C2",
            "Atilde" => "\u00C3",
            "Auml" => "\u00C4",
            "Aring" => "\u00C5",
            "AElig" => "\u00C6",
            "Ccedil" => "\u00C7",
            "Egrave" => "\u00C8",
            "Eacute" => "\u00C9",
            "Ecirc" => "\u00CA",
            "Euml" => "\u00CB",
            "Igrave" => "\u00CC",
            "Iacute" => "\u00CD",
            "Icirc" => "\u00CE",
            "Iuml" => "\u00CF",
            "ETH" => "\u00D0",
            "Ntilde" => "\u00D1",
            "Ograve" => "\u00D2",
            "Oacute" => "\u00D3",
            "Ocirc" => "\u00D4",
            "Otilde" => "\u00D5",
            "Ouml" => "\u00D6",
            "times" => "\u00D7",
            "Oslash" => "\u00D8",
            "Ugrave" => "\u00D9",
            "Uacute" => "\u00DA",
            "Ucirc" => "\u00DB",
            "Uuml" => "\u00DC",
            "Yacute" => "\u00DD",
            "THORN" => "\u00DE",
            "szlig" => "\u00DF",
            "agrave" => "\u00E0",
            "aacute" => "\u00E1",
            "acirc" => "\u00E2",
            "atilde" => "\u00E3",
            "auml" => "\u00E4",
            "aring" => "\u00E5",
            "aelig" => "\u00E6",
            "ccedil" => "\u00E7",
            "egrave" => "\u00E8",
            "eacute" => "\u00E9",
            "ecirc" => "\u00EA",
            "euml" => "\u00EB",
            "igrave" => "\u00EC",
            "iacute" => "\u00ED",
            "icirc" => "\u00EE",
            "iuml" => "\u00EF",
            "eth" => "\u00F0",
            "ntilde" => "\u00F1",
            "ograve" => "\u00F2",
            "oacute" => "\u00F3",
            "ocirc" => "\u00F4",
            "otilde" => "\u00F5",
            "ouml" => "\u00F6",
            "divide" => "\u00F7",
            "oslash" => "\u00F8",
            "ugrave" => "\u00F9",
            "uacute" => "\u00FA",
            "ucirc" => "\u00FB",
            "uuml" => "\u00FC",
            "yacute" => "\u00FD",
            "thorn" => "\u00FE",
            "yuml" => "\u00FF",
            "OElig" => "\u0152",
            "oelig" => "\u0153",
            "Scaron" => "\u0160",
            "scaron" => "\u0161",
            "Yuml" => "\u0178",
            "fnof" => "\u0192",
            "circ" => "\u02C6",
            "tilde" => "\u02DC",
            "Alpha" => "\u0391",
            "Beta" => "\u0392",
            "Gamma" => "\u0393",
            "Delta" => "\u0394",
            "Epsilon" => "\u0395",
            "Zeta" => "\u0396",
            "Eta" => "\u0397",
            "Theta" => "\u0398",
            "Iota" => "\u0399",
            "Kappa" => "\u039A",
            "Lambda" => "\u039B",
            "Mu" => "\u039C",
            "Nu" => "\u039D",
            "Xi" => "\u039E",
            "Omicron" => "\u039F",
            "Pi" => "\u03A0",
            "Rho" => "\u03A1",
            "Sigma" => "\u03A3",
            "Tau" => "\u03A4",
            "Upsilon" => "\u03A5",
            "Phi" => "\u03A6",
            "Chi" => "\u03A7",
            "Psi" => "\u03A8",
            "Omega" => "\u03A9",
            "alpha" => "\u03B1",
            "beta" => "\u03B2",
            "gamma" => "\u03B3",
            "delta" => "\u03B4",
            "epsilon" => "\u03B5",
            "zeta" => "\u03B6",
            "eta" => "\u03B7",
            "theta" => "\u03B8",
            "iota" => "\u03B9",
            "kappa" => "\u03BA",
            "lambda" => "\u03BB",
            "mu" => "\u03BC",
            "nu" => "\u03BD",
            "xi" => "\u03BE",
            "omicron" => "\u03BF",
            "pi" => "\u03C0",
            "rho" => "\u03C1",
            "sigmaf" => "\u03C2",
            "sigma" => "\u03C3",
            "tau" => "\u03C4",
            "upsilon" => "\u03C5",
            "phi" => "\u03C6",
            "chi" => "\u03C7",
            "psi" => "\u03C8",
            "omega" => "\u03C9",
            "thetasym" => "\u03D1",
            "upsih" => "\u03D2",
            "piv" => "\u03D6",
            "ensp" => "\u2002",
            "emsp" => "\u2003",
            "thinsp" => "\u2009",
            "zwnj" => "\u200C",
            "zwj" => "\u200D",
            "lrm" => "\u200E",
            "rlm" => "\u200F",
            "ndash" => "\u2013",
            "mdash" => "\u2014",
            "lsquo" => "\u2018",
            "rsquo" => "\u2019",
            "sbquo" => "\u201A",
            "ldquo" => "\u201C",
            "rdquo" => "\u201D",
            "bdquo" => "\u201E",
            "dagger" => "\u2020",
            "Dagger" => "\u2021",
            "bull" => "\u2022",
            "hellip" => "\u2026",
            "permil" => "\u2030",
            "prime" => "\u2032",
            "Prime" => "\u2033",
            "lsaquo" => "\u2039",
            "rsaquo" => "\u203A",
            "oline" => "\u203E",
            "frasl" => "\u2044",
            "weierp" => "\u2118",
            "image" => "\u2111",
            "real" => "\u211C",
            "trade" => "\u2122",
            "alefsym" => "\u2135",
            "larr" => "\u2190",
            "uarr" => "\u2191",
            "rarr" => "\u2192",
            "darr" => "\u2193",
            "harr" => "\u2194",
            "crarr" => "\u21B5",
            "lArr" => "\u21D0",
            "uArr" => "\u21D1",
            "rArr" => "\u21D2",
            "dArr" => "\u21D3",
            "hArr" => "\u21D4",
            "forall" => "\u2200",
            "part" => "\u2202",
            "exist" => "\u2203",
            "empty" => "\u2205",
            "nabla" => "\u2207",
            "isin" => "\u2208",
            "notin" => "\u2209",
            "ni" => "\u220B",
            "prod" => "\u220F",
            "sum" => "\u2211",
            "minus" => "\u2212",
            "lowast" => "\u2217",
            "radic" => "\u221A",
            "prop" => "\u221D",
            "infin" => "\u221E",
            "ang" => "\u2220",
            "and" => "\u2227",
            "or" => "\u2228",
            "cap" => "\u2229",
            "cup" => "\u222A",
            "int" => "\u222B",
            "there4" => "\u2234",
            "sim" => "\u223C",
            "cong" => "\u2245",
            "asymp" => "\u2248",
            "ne" => "\u2260",
            "equiv" => "\u2261",
            "le" => "\u2264",
            "ge" => "\u2265",
            "sub" => "\u2282",
            "sup" => "\u2283",
            "nsub" => "\u2284",
            "sube" => "\u2286",
            "supe" => "\u2287",
            "oplus" => "\u2295",
            "otimes" => "\u2297",
            "perp" => "\u22A5",
            "sdot" => "\u22C5",
            "lceil" => "\u2308",
            "rceil" => "\u2309",
            "lfloor" => "\u230A",
            "rfloor" => "\u230B",
            "loz" => "\u25CA",
            "spades" => "\u2660",
            "clubs" => "\u2663",
            "hearts" => "\u2665",
            "diams" => "\u2666",
            _ => null
        };
    }

    #endregion

    /// <summary>
    /// Parses Markdown from a stream.
    /// </summary>
    /// <param name="stream">The stream to parse from</param>
    /// <param name="leaveOpen">true to leave the stream open after parsing completes; otherwise, false (default false). The stream will be closed when parsing completes (or if an exception occurs) unless leaveOpen is true.</param>
    /// <returns>The parsed Markdown document</returns>
    public static MarkdownDocumentNode Parse(Stream stream, bool leaveOpen = false)
    {
        using var parser = new MarkdownParser(stream, leaveOpen: leaveOpen);
        return parser.Parse();
    }

    /// <summary>
    /// Parses Markdown from a byte array
    /// </summary>
    public static MarkdownDocumentNode Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return Parse(stream);
    }

    /// <summary>
    /// Parses Markdown from a string
    /// </summary>
    public static MarkdownDocumentNode Parse(string markdown)
    {
        var bytes = Encoding.UTF8.GetBytes(markdown);
        return Parse(bytes);
    }
}