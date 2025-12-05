using System.Text;
using Femur.Parsing;
using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Parser;
using Femur.Xml.Abstractions;

namespace Femur.Html.Parser;

/// <summary>
/// Streaming HTML 2.0 parser that reads from a Stream and builds an AST of nodes.
/// 
/// PARSING STRATEGY:
/// - Uses a sliding buffer to read stream in chunks (default 4KB)
/// - Tracks absolute position across buffer boundaries for location tracking
/// - Maintains element stack to match opening/closing tags
/// - Processes tokens in a single pass (no separate tokenization phase)
/// </summary>
public class HtmlParser : StreamParser<DocumentNode>
{
    // HTML void elements that cannot have children and are self-closing by default
    // These don't need closing tags and shouldn't be pushed onto the element stack
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    };

    private DocumentNode? _document;
    private ContainerNode? _currentParent;
    private Stack<ElementNode>? _elementStack;
    private bool _isInsideScriptOrStyle;

    /// <summary>
    /// Creates a new HTML parser for the given stream
    /// </summary>
    /// <param name="stream">The stream to parse</param>
    /// <param name="bufferSize">Size of the buffer for reading chunks (default 4096)</param>
    /// <param name="leaveOpen">true to leave the stream open after the parser is disposed; otherwise, false (default false)</param>
    public HtmlParser(Stream stream, int bufferSize = 4096, bool leaveOpen = false) : base(stream, bufferSize, leaveOpen)
    {
    }

    /// <summary>
    /// Creates a new document instance
    /// </summary>
    protected override DocumentNode CreateDocument()
    {
        return new DocumentNode();
    }

    /// <summary>
    /// Initializes parsing state (stacks, flags, etc.)
    /// </summary>
    protected override void InitializeParsing(DocumentNode document)
    {
        // Store document reference for use in ProcessClosingTag
        this._document = document;

        // Track current parent node for adding children
        // Start with document as root parent (DocumentNode is a container)
        this._currentParent = document;

        // Stack to track opening tags for matching with closing tags
        // Allows us to handle nested elements correctly
        this._elementStack = new Stack<ElementNode>();

        // Track if we're inside script/style tags where content should be preserved as-is
        this._isInsideScriptOrStyle = false;
    }

    /// <summary>
    /// Processes a single character from the stream
    /// </summary>
    protected override void ProcessCharacter(char ch, DocumentNode document)
    {
        // Tags start with '<' - route to tag processing
        if (ch == '<')
        {
            this.ProcessTag();
        }
        // Everything else is text content until we hit a '<'
        else
        {
            this.ProcessTextContent();
        }
    }

    /// <summary>
    /// Helper method to add a child node and update sibling references.
    /// Maintains bidirectional sibling links during parsing.
    /// </summary>
    private void AddChildWithSiblings(Node child, ContainerNode? parent)
    {
        if (parent != null)
        {
            parent.Children.Add(child);
            parent.UpdateSiblingReferences();
        }
    }

    /// <summary>
    /// Processes a tag (opening, closing, or special tag like comment/CDATA)
    /// 
    /// After detecting '&lt;', we examine the next character to determine tag type:
    /// - '!' - Special tag (comment, CDATA, DOCTYPE)
    /// - '/' - Closing tag
    /// - Otherwise - Opening tag
    /// </summary>
    private void ProcessTag()
    {
        // Advance past '<' character we already detected
        this.Position++;

        // Ensure we have data to read the next character after '<'
        if (!this.ReadMore())
        {
            return;
        }

        // Peek at character after '<' to determine tag type
        var nextChar = this.Buffer[this.Position];

        // Special tags: <!-- comment -->, <![CDATA[...]]>, <!DOCTYPE>
        if (nextChar == '!')
        {
            this.ProcessSpecialTag();
        }
        // Closing tags: </tag>
        else if (nextChar == '/')
        {
            this.ProcessClosingTag();
        }
        // Opening tags: <tag> or <tag />
        else
        {
            this.ProcessOpeningTag();
        }
    }

    /// <summary>
    /// Processes a special tag (comment, CDATA, or DOCTYPE)
    /// These tags don't affect the element hierarchy, so currentParent stays unchanged
    /// </summary>
    private void ProcessSpecialTag()
    {
        // Track start position including the '<' character
        var startPos = this.GetAbsolutePosition() - 1; // -1 to include '<'

        // Advance past '!' character
        this.Position++;

        // Parse the special tag (handles comment, CDATA, or DOCTYPE)
        var node = this.ParseSpecialTag(startPos);

        // Add to current parent's children (doesn't change currentParent)
        if (node != null)
        {
            node.SetParent(this._currentParent);
            this.AddChildWithSiblings(node, this._currentParent);
            this.OnNodeCreated(node);
        }
    }

    /// <summary>
    /// Processes a closing tag and updates the parent stack
    /// 
    /// When we encounter &lt;/tag&gt;, we need to:
    /// 1. Parse the tag name
    /// 2. Find the matching opening tag in the stack
    /// 3. Pop elements from stack until we find a match
    /// 4. Update currentParent to the matched element's parent
    /// 
    /// Note: We handle mismatched closing tags gracefully by popping up the stack
    /// until we find a match (similar to how browsers handle malformed HTML)
    /// </summary>
    private void ProcessClosingTag()
    {
        // Advance past '/' character
        this.Position++;

        // Parse the tag name
        var closingTagName = this.ParseClosingTag();
        if (string.IsNullOrEmpty(closingTagName))
        {
            return;
        }

        // Find matching opening tag in stack
        // We pop elements until we find a match, handling mismatched tags
        var foundMatch = false;
        ElementNode? matchedElement = null;

        while (this._elementStack!.Count > 0)
        {
            var topElement = this._elementStack.Pop();

            // Found matching tag - remember it and stop popping
            if (string.Equals(topElement.TagName, closingTagName, StringComparison.OrdinalIgnoreCase))
            {
                matchedElement = topElement;
                foundMatch = true;
                break;
            }

            // Tag doesn't match - this closing tag doesn't match the most recent opening tag
            // Continue up the stack looking for a match (handles malformed HTML)
            // We update currentParent as we go up, but will override it if we find a match
            this._currentParent = topElement.GetParent() as ContainerNode;
        }

        // If we found a match, restore currentParent to the matched element's parent
        // This ensures subsequent sibling elements are added to the correct parent
        if (foundMatch && matchedElement != null)
        {
            this._currentParent = matchedElement.GetParent() as ContainerNode;

            // Track if we're exiting a script or style tag
            if (string.Equals(matchedElement.TagName, "script", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(matchedElement.TagName, "style", StringComparison.OrdinalIgnoreCase))
            {
                this._isInsideScriptOrStyle = false;
            }

            // Note: SVG and MathML closing tags are handled in ProcessOpeningTag, not here
            // because we need to rewind and parse the entire <svg>...</svg> or <math>...</math> block as XML
        }
        // If we didn't find a match and stack is empty, restore to document root
        // This ensures currentParent is always valid for subsequent sibling elements
        if (!foundMatch && this._elementStack.Count == 0)
        {
            this._currentParent = this._document;
        }

        // Defensive check: ensure currentParent is never null
        this._currentParent ??= this._document;
    }

    /// <summary>
    /// Processes an opening tag and updates the parent stack
    /// 
    /// When we encounter &lt;tag&gt;, we:
    /// 1. Parse the tag name and attributes
    /// 2. Create an ElementNode and add it to current parent's children
    /// 3. If tag is not self-closing and not void, push it onto the stack
    /// 4. Update currentParent to the new element (so children go here)
    /// 
    /// Void elements (br, img, etc.) and self-closing tags don't go on the stack
    /// because they can't have children and don't need matching closing tags
    /// </summary>
    private void ProcessOpeningTag()
    {
        // Track start position including the '<' character
        var startPos = this.GetAbsolutePosition() - 1; // -1 to include '<'

        // Peek ahead to check if this is an SVG or MathML tag before parsing
        var savedPosition = this.Position;

        var tagName = this.ReadUntilAny([' ', '\t', '\r', '\n', '/', '>'], out _);
        this.Position = savedPosition; // Restore position

        // Check if this is a foreign element (SVG or MathML) - if so, parse entire block as XML immediately
        if (string.Equals(tagName, "svg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tagName, "math", StringComparison.OrdinalIgnoreCase))
        {
            // Rewind to the opening tag position
            var currentAbsolutePos = this.GetAbsolutePosition();
            var bytesToRewind = currentAbsolutePos - startPos;

            if (bytesToRewind > 0)
            {
                // Check if the start position is in a previous buffer (already consumed)
                // If startPos < TotalCharsRead, it means the start is in a buffer we've already discarded
                if (startPos < this.TotalCharsRead)
                {
                    // Can't rewind past buffer boundary - would need to seek stream
                    // Foreign element blocks that span multiple buffers are not currently supported
                    // This is a limitation of the current implementation
                    throw new InvalidOperationException(
                        $"Cannot rewind to {tagName} start position: {tagName} block spans multiple buffers. " +
                        $"Start position: {startPos}, Current position: {currentAbsolutePos}, " +
                        $"TotalCharsRead: {this.TotalCharsRead}, Current buffer position: {this.Position}. " +
                        $"{tagName} blocks must fit within a single buffer (typically 4KB).");
                }

                // If we can rewind within current buffer, do it
                if (bytesToRewind <= this.Position)
                {
                    this.Position -= bytesToRewind;
                }
                else
                {
                    // Can't rewind past buffer boundary - would need to seek stream
                    // Foreign element blocks that span multiple buffers are not currently supported
                    // This is a limitation of the current implementation
                    throw new InvalidOperationException(
                        $"Cannot rewind to {tagName} start position: {tagName} block spans multiple buffers. " +
                        $"Start position: {startPos}, Current position: {currentAbsolutePos}, " +
                        $"Bytes to rewind: {bytesToRewind}, Current buffer position: {this.Position}. " +
                        $"{tagName} blocks must fit within a single buffer (typically 4KB).");
                }
            }

            // Parse the entire foreign element block as XML
            this.ParseForeignElementAsXml(startPos, tagName);
            return;
        }

        // Parse the opening tag (name, attributes, self-closing indicator)
        var element = this.ParseOpeningTag(startPos);
        if (element == null)
        {
            return;
        }

        // Set parent-child relationship
        element.SetParent(this._currentParent);
        this.AddChildWithSiblings(element, this._currentParent);
        this.OnNodeCreated(element);

        // Only push non-void, non-self-closing elements onto stack
        // These elements can have children and need matching closing tags
        if (!element.IsSelfClosing && !element.IsVoidElement)
        {
            this._elementStack!.Push(element);

            this._currentParent = element; // New children will be added to this element

            // Track if we're entering a script or style tag
            if (string.Equals(element.TagName, "script", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(element.TagName, "style", StringComparison.OrdinalIgnoreCase))
            {
                this._isInsideScriptOrStyle = true;
            }
        }
    }

    /// <summary>
    /// Processes text content until a tag is encountered
    /// 
    /// Text content is everything between tags. We:
    /// 1. Parse all characters until we hit '&lt;' (start of next tag)
    /// 2. Filter out pure whitespace text nodes (but keep the content if it has any non-whitespace)
    /// 3. Create TextNode with location tracking
    /// 
    /// Note: We preserve whitespace in the content but only create nodes for text with actual content
    /// Inside script/style tags, we preserve ALL content including whitespace-only text
    /// </summary>
    private void ProcessTextContent()
    {
        // Track where text starts for location information
        var startPos = this.GetAbsolutePosition();

        // Read all characters until we hit '<' (which starts the next tag)
        var text = this.ParseText(this._isInsideScriptOrStyle);

        // Inside script/style tags, preserve ALL content including whitespace-only text
        // Outside script/style tags, filter out pure whitespace text nodes (indentation/formatting)
        if (!this._isInsideScriptOrStyle)
        {
            var trimmed = text.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }
        }
        // For script/style tags, always create text node even if it's just whitespace
        // This preserves the exact script content including all characters

        // Calculate end position and create text node with location
        var endPos = this.GetAbsolutePosition();
        var textNode = new TextNode
        {
            Content = text, // Preserve original content including whitespace
            Location = new SourceLocation(startPos, endPos - startPos)
        };
        textNode.SetParent(this._currentParent);
        this.AddChildWithSiblings(textNode, this._currentParent);
        this.OnNodeCreated(textNode);
    }


    /// <summary>
    /// Parses an opening HTML tag: &lt;tag attr="value" /&gt;
    /// 
    /// PROCESSING FLOW:
    /// 1. Read tag name (until whitespace, '/', or '&gt;')
    /// 2. Check if void element (br, img, etc.) - these can't have children
    /// 3. Handle self-closing tags (&lt;tag /&gt; or &lt;tag/&gt;)
    /// 4. Parse attributes if present:
    ///    - Attribute name until '=', whitespace, or '&gt;'
    ///    - Attribute value (quoted or unquoted)
    ///    - Boolean attributes (no value)
    /// 5. Set location tracking for the element
    /// 
    /// ATTRIBUTE HANDLING:
    /// - Quoted values: "value" or 'value'
    /// - Unquoted values: value (until whitespace or '&gt;')
    /// - Boolean attributes: attr (no value)
    /// </summary>
    private ElementNode? ParseOpeningTag(int startPos)
    {
        if (!this.ReadMore())
        {
            return null;
        }

        // Read tag name until we hit whitespace, '/', or '>'
        var tagName = this.ReadUntilAny([' ', '\t', '\r', '\n', '/', '>'], out var delimiter);
        if (string.IsNullOrEmpty(tagName))
        {
            return null;
        }

        var element = new ElementNode { TagName = tagName };

        // Mark void elements (these can't have children and don't need closing tags)
        element.IsVoidElement = VoidElements.Contains(tagName);

        // Handle self-closing tags: <tag />
        if (delimiter == '/')
        {
            element.IsSelfClosing = true;
            // Skip the '>' that follows '/'
            if (this.Position < this.Length && this.Buffer[this.Position] == '>')
            {
                this.Position++;
            }

            var endPos = this.GetAbsolutePosition();
            element.Location = new SourceLocation(startPos, endPos - startPos);
            return element;
        }

        // Handle tags without attributes: <tag>
        if (delimiter == '>')
        {
            var endPos = this.GetAbsolutePosition();
            element.Location = new SourceLocation(startPos, endPos - startPos);
            return element;
        }

        // Parse attributes (tag has attributes or whitespace before '>')
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            // Skip whitespace between attributes
            this.SkipWhitespace();

            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];

            // End of tag
            if (ch == '>')
            {
                this.Position++;
                break;
            }

            // Self-closing tag: <tag attr="value" />
            if (ch == '/')
            {
                this.Position++;
                element.IsSelfClosing = true;
                if (this.Position < this.Length && this.Buffer[this.Position] == '>')
                {
                    this.Position++;
                    break;
                }

                continue;
            }

            // Parse attribute name
            var attrName = this.ReadUntilAny(['=', ' ', '\t', '\r', '\n', '/', '>'], out var attrDelimiter);
            if (string.IsNullOrEmpty(attrName))
            {
                break;
            }

            // Boolean attribute without value: <tag attr>
            if (attrDelimiter == '>')
            {
                element.Attributes[attrName] = string.Empty;
                break;
            }

            // Boolean attribute: <tag attr >
            if (attrDelimiter != '=')
            {
                element.Attributes[attrName] = string.Empty;
                continue;
            }

            // Attribute has a value - parse it
            // Skip whitespace after '='
            this.SkipWhitespace();

            if (this.Position >= this.Length)
            {
                break;
            }

            ch = this.Buffer[this.Position];
            string attrValue;

            // Quoted attribute value: attr="value" or attr='value'
            if (ch == '"' || ch == '\'')
            {
                this.Position++; // Skip opening quote
                attrValue = this.ReadUntil(ch); // Read until matching quote
            }
            // Unquoted attribute value: attr=value
            else
            {
                attrValue = this.ReadUntilAny([' ', '\t', '\r', '\n', '/', '>'], out _);
            }

            element.Attributes[attrName] = attrValue;
        }

        var finalEndPos = this.GetAbsolutePosition();
        element.Location = new SourceLocation(startPos, finalEndPos - startPos);
        return element;
    }

    /// <summary>
    /// Parses a closing tag: &lt;/tag&gt;
    /// 
    /// Reads the tag name and consumes everything until the closing '&gt;'.
    /// Returns the tag name for matching against the element stack.
    /// </summary>
    private string ParseClosingTag()
    {
        // Read tag name (everything until whitespace or '>')
        // ReadUntilAny will consume the stop character ('>'), so we're positioned right after it
        var tagName = this.ReadUntilAny([' ', '\t', '\r', '\n', '>'], out var matchedChar);

        // If we matched '>', ReadUntilAny already consumed it, so we're done
        // Otherwise, skip any remaining whitespace until we find the closing '>'
        if (matchedChar != '>')
        {
            while (this.Position < this.Length || this.ReadMore())
            {
                if (this.Position >= this.Length)
                {
                    break;
                }

                if (this.Buffer[this.Position] == '>')
                {
                    this.Position++;
                    break;
                }

                this.Position++;
            }
        }

        return tagName;
    }

    /// <summary>
    /// Parses a special tag after '&lt;!': comments, CDATA, or DOCTYPE
    /// 
    /// SPECIAL TAGS:
    /// - Comments: &lt;!-- comment --&gt;
    /// - CDATA: &lt;![CDATA[...]]&gt;
    /// - DOCTYPE: &lt;!DOCTYPE ...&gt;
    /// 
    /// Returns appropriate node type based on the tag structure.
    /// </summary>
    private Node? ParseSpecialTag(int startPos)
    {
        if (!this.ReadMore())
        {
            return null;
        }

        var ch = this.Buffer[this.Position];

        if (ch == '-')
        {
            return this.ParseCommentTag(startPos);
        }
        else if (ch == '[')
        {
            return this.ParseCDataTag(startPos);
        }
        else
        {
            return this.ParseDocumentTypeTag(startPos);
        }
    }

    /// <summary>
    /// Parses a comment tag: &lt;!-- comment --&gt;
    /// 
    /// Format: <!-- (dash dash) content (dash dash) >
    /// Handles dashes in comment content carefully to distinguish between
    /// closing dashes (-->) and dashes that are part of the content.
    /// </summary>
    private CommentNode? ParseCommentTag(int startPos)
    {
        // Comment: <!-- ... -->
        // Format: <!-- (dash dash) content (dash dash) >
        this.Position++; // Skip second '-'
        _ = this.StringBuilder.Clear();
        var dashCount = 0;

        // Read comment content, handling dashes in content carefully
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var c = this.Buffer[this.Position];

            if (c == '-')
            {
                dashCount++;
                // Check if we have "--" followed by '>'
                if (dashCount >= 2)
                {
                    this.Position++;
                    if (this.Position < this.Length || this.ReadMore())
                    {
                        if (this.Position < this.Length && this.Buffer[this.Position] == '>')
                        {
                            this.Position++;
                            break; // Found closing -->
                        }
                    }
                    // Not a closing - this is dashes in the comment content
                    // Add all but one dash to content, keep one for next check
                    _ = this.StringBuilder.Append(new string('-', dashCount - 1));
                    dashCount = 1;
                    continue;
                }
            }
            else
            {
                // Not a dash - flush any accumulated dashes and add character
                if (dashCount > 0)
                {
                    _ = this.StringBuilder.Append(new string('-', dashCount));
                    dashCount = 0;
                }

                _ = this.StringBuilder.Append(c);
            }

            this.Position++;
        }

        // Clean up trailing dashes if comment ended abruptly
        var content = this.StringBuilder.ToString();
        if (content.EndsWith("--", StringComparison.OrdinalIgnoreCase))
        {
            content = content.Substring(0, content.Length - 2);
        }

        var endPos = this.GetAbsolutePosition();
        return new CommentNode
        {
            Content = content,
            Location = new SourceLocation(startPos, endPos - startPos)
        };
    }

    /// <summary>
    /// Parses a CDATA tag: &lt;![CDATA[...]]&gt;
    /// 
    /// Format: &lt;![CDATA[ content ]]&gt;
    /// Handles closing by matching two ']' followed by '&gt;'.
    /// </summary>
    private CDataNode? ParseCDataTag(int startPos)
    {
        // CDATA: <![CDATA[...]]>
        // Format: <![CDATA[ content ]]>
        var cdataStart = "[CDATA[";
        var i = 0;

        // Verify we have the full "[CDATA[" marker
        while (i < cdataStart.Length && (this.Position + i < this.Length || this.ReadMore()))
        {
            if (this.Position + i >= this.Length)
            {
                break;
            }

            if (this.Buffer[this.Position + i] != cdataStart[i])
            {
                return null; // Invalid CDATA format
            }

            i++;
        }

        // Skip past "[CDATA["
        this.Position += cdataStart.Length;

        // Read content until we find ']'
        var content = this.ReadUntil(']');

        // Handle closing "]]>" - need to match two ']' followed by '>'
        var closing = 0;
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var c = this.Buffer[this.Position];
            if (c == ']')
            {
                closing++;
                this.Position++;
            }
            else if (c == '>' && closing >= 2)
            {
                // Found closing ]]>
                this.Position++;
                break;
            }
            else
            {
                // Not closing - add accumulated ']' and current char to content
                content += new string(']', closing);
                closing = 0;
                content += c;
                this.Position++;
            }
        }

        var endPos = this.GetAbsolutePosition();
        return new CDataNode
        {
            Content = content,
            Location = new SourceLocation(startPos, endPos - startPos)
        };
    }

    /// <summary>
    /// Parses a DOCTYPE tag: &lt;!DOCTYPE ...&gt;
    /// 
    /// Reads everything until the closing '>' character.
    /// </summary>
    private DocumentTypeNode ParseDocumentTypeTag(int startPos)
    {
        // DOCTYPE: <!DOCTYPE ...>
        // Read everything until '>'
        var doctype = this.ReadUntil('>');
        var endPos = this.GetAbsolutePosition();
        return new DocumentTypeNode
        {
            Content = doctype,
            Location = new SourceLocation(startPos, endPos - startPos)
        };
    }

    /// <summary>
    /// Parses text content until a tag is encountered
    /// 
    /// Text is everything between tags. This method:
    /// - Reads all characters until '&lt;' (start of next tag)
    /// - Handles multi-buffer reads if needed
    /// - Stops at '&lt;' without consuming it
    /// 
    /// The '&lt;' character is not consumed so ProcessTag can handle it correctly.
    /// </summary>
    private string ParseText(bool isInsideScriptOrStyle = false)
    {
        var chars = new List<char>();
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];

            // Stop at '<' - but inside script/style tags, only stop if it's a closing tag
            if (ch == '<')
            {
                if (!isInsideScriptOrStyle)
                {
                    // Not inside script/style - stop at '<' to process as tag
                    break;
                }
                else
                {
                    // Inside script/style - check if this is a closing tag
                    // Peek ahead to see if it's </script> or </style>
                    var savedPosition = this.Position;
                    var isClosingTag = false;

                    // Check if next character is '/'
                    if (this.Position + 1 < this.Length || this.ReadMore())
                    {
                        if (this.Position + 1 < this.Length)
                        {
                            var nextCh = this.Buffer[this.Position + 1];
                            if (nextCh == '/')
                            {
                                // It might be a closing tag - check the tag name
                                // Read characters manually to avoid consuming them
                                var tagStart = this.Position + 2; // After '</'
                                var tagEnd = tagStart;

                                // Find where the tag name ends (space, tab, newline, or >)
                                while (tagEnd < this.Length || this.ReadMore())
                                {
                                    if (tagEnd >= this.Length)
                                    {
                                        break;
                                    }

                                    var tagCh = this.Buffer[tagEnd];
                                    if (tagCh is ' ' or '\t' or '\r' or '\n' or '>')
                                    {
                                        break;
                                    }

                                    tagEnd++;
                                }

                                if (tagEnd > tagStart)
                                {
                                    // Extract tag name from char buffer
                                    var tagName = new string(this.Buffer, tagStart, tagEnd - tagStart);

                                    // Check if it's the closing script/style tag
                                    if (string.Equals(tagName, "script", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(tagName, "style", StringComparison.OrdinalIgnoreCase))
                                    {
                                        isClosingTag = true;
                                    }
                                }
                            }
                        }
                    }

                    if (isClosingTag)
                    {
                        // It's the closing tag - restore position and break
                        this.Position = savedPosition;
                        break;
                    }
                    // Not a closing script/style tag - treat '<' as literal text and continue
                }
            }

            // Accumulate characters
            chars.Add(ch);
            this.Position++;
        }

        // Convert accumulated characters to string
        return new string(chars.ToArray());
    }

    /// <summary>
    /// Parses a foreign element (SVG or MathML) block as XML using continuous stream parsing.
    /// 
    /// Assumes the parser position has already been rewound to the opening tag.
    /// Parses the entire block as XML and adds the resulting XmlElementNode to the HTML AST.
    /// </summary>
    /// <param name="startAbsolutePosition">The absolute position of the opening tag</param>
    /// <param name="tagName">The tag name (e.g., "svg" or "math")</param>
    private void ParseForeignElementAsXml(int startAbsolutePosition, string tagName)
    {
        // Create a stream wrapper that reads from the opening tag position
        // until the closing tag, advancing the parent parser's position as it reads
        var foreignElementStream = new ForeignElementSubStream(this, startAbsolutePosition, tagName);

        try
        {
            // Parse entire foreign element block as XML
            var xmlParser = new XmlParser(foreignElementStream);
            var xmlDocument = xmlParser.Parse();

            // Get the root foreign element from the XML document
            // XmlParser creates a document with the foreign element as its child
            var xmlElement = xmlDocument.Children.OfType<XmlElementNode>().FirstOrDefault();
            if (xmlElement != null)
            {
                // Set parent and add to HTML AST
                xmlElement.SetParent(this._currentParent);
                this.AddChildWithSiblings(xmlElement, this._currentParent);
                this.OnNodeCreated(xmlElement);

                // Foreign elements (SVG/MathML) are not pushed onto the stack
                // HTML elements after foreign elements should be siblings, not children
                // The foreign element is self-contained and doesn't affect HTML parsing context
            }

            // Sync parent parser position to where XmlParser stopped
            // ForeignElementSubStream has already advanced the position past the closing tag
            this.Position = foreignElementStream.GetFinalPosition();
            this.TotalCharsRead = foreignElementStream.GetFinalTotalCharsRead();
        }
        finally
        {
            foreignElementStream.Dispose();
        }
    }

    /// <summary>
    /// A stream wrapper that reads from a specific absolute position of an HTML parser
    /// until it encounters a closing foreign element tag (e.g., &lt;/svg&gt; or &lt;/math&gt;).
    /// 
    /// This allows XmlParser to parse foreign element content while advancing the parent stream position.
    /// Nested class so it can access HtmlParser's protected members.
    /// </summary>
    private sealed class ForeignElementSubStream : Stream
    {
        private readonly HtmlParser _parentParser;
        private readonly int _startAbsolutePosition;
        private readonly string _tagName;
        private int _bytesRead;
        private bool _endReached;
        private readonly StringBuilder _tagBuffer = new();
        private bool _inTag;
        private int _elementDepth;
        private int _finalPosition;
        private int _finalTotalCharsRead;

        public ForeignElementSubStream(HtmlParser parentParser, int startAbsolutePosition, string tagName)
        {
            this._parentParser = parentParser;
            this._startAbsolutePosition = startAbsolutePosition;
            this._tagName = tagName;
            this._elementDepth = 1; // We're already inside the opening tag

            // Rewind parent parser to start position
            this.RewindToStart();
        }

        private void RewindToStart()
        {
            var currentAbsolutePos = this._parentParser.GetAbsolutePosition();
            var bytesToRewind = currentAbsolutePos - this._startAbsolutePosition;

            if (bytesToRewind > 0)
            {
                // If we can rewind within current buffer, do it
                if (bytesToRewind <= this._parentParser.Position)
                {
                    this._parentParser.Position -= bytesToRewind;
                }
                else
                {
                    // Can't rewind past buffer boundary - would need to seek stream
                    // Foreign element blocks that span multiple buffers are not currently supported
                    // This is a limitation of the current implementation
                    throw new InvalidOperationException(
                        $"Cannot rewind ForeignElementSubStream to start position: {this._tagName} block spans multiple buffers. " +
                        $"Start position: {this._startAbsolutePosition}, Current position: {currentAbsolutePos}, " +
                        $"Bytes to rewind: {bytesToRewind}, Current buffer position: {this._parentParser.Position}. " +
                        $"{this._tagName} blocks must fit within a single buffer (typically 4KB).");
                }
            }
        }

        public override bool CanRead => !this._endReached;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this._endReached)
            {
                return 0;
            }

            var bytesRead = 0;
            var bufferPos = 0;
            var pendingTagChars = new List<char>(); // Buffer for characters in a tag until we confirm it's not the closing tag

            while (bufferPos < count && !this._endReached)
            {
                // Ensure parent parser has data
                if (!this._parentParser.ReadMore())
                {
                    // If we have pending tag characters, flush them
                    if (pendingTagChars.Count > 0)
                    {
                        var charArray = pendingTagChars.ToArray();
                        var bytes = Encoding.UTF8.GetBytes(charArray);
                        foreach (var b in bytes)
                        {
                            if (bufferPos >= count)
                            {
                                break;
                            }

                            buffer[bufferPos + offset] = b;
                            bufferPos++;
                            bytesRead++;
                        }

                        pendingTagChars.Clear();
                    }

                    this._endReached = true;
                    break;
                }

                var currentChar = this._parentParser.Buffer[this._parentParser.Position];

                // Check if we're entering a tag
                if (currentChar == '<')
                {
                    this._inTag = true;
                    _ = this._tagBuffer.Clear();
                    _ = this._tagBuffer.Append(currentChar);
                    pendingTagChars.Add(currentChar); // Buffer this character
                    this._parentParser.Position++;
                }
                else if (this._inTag)
                {
                    _ = this._tagBuffer.Append(currentChar);
                    pendingTagChars.Add(currentChar); // Buffer this character
                    this._parentParser.Position++;

                    // Check for closing foreign element tag
                    if (currentChar == '>')
                    {
                        var tag = this._tagBuffer.ToString().Trim();
                        var closingTag = $"</{this._tagName}>";
                        var closingTagStart = $"</{this._tagName}";
                        var openingTag = $"<{this._tagName}";

                        if (tag.Equals(closingTag, StringComparison.OrdinalIgnoreCase) ||
                            tag.StartsWith(closingTagStart, StringComparison.OrdinalIgnoreCase))
                        {
                            // Found closing tag - don't copy buffered characters, stop reading
                            this._finalPosition = this._parentParser.Position;
                            this._finalTotalCharsRead = this._parentParser.TotalCharsRead;
                            this._endReached = true;
                            pendingTagChars.Clear(); // Discard the closing tag
                            break;
                        }
                        else
                        {
                            // Not a closing tag - flush buffered characters to output
                            var charArray = pendingTagChars.ToArray();
                            var bytes = Encoding.UTF8.GetBytes(charArray);
                            foreach (var b in bytes)
                            {
                                if (bufferPos >= count)
                                {
                                    break;
                                }

                                buffer[bufferPos + offset] = b;
                                bufferPos++;
                                bytesRead++;
                            }

                            pendingTagChars.Clear();

                            if (tag.StartsWith(openingTag, StringComparison.OrdinalIgnoreCase) &&
                                !tag.StartsWith(closingTagStart, StringComparison.OrdinalIgnoreCase))
                            {
                                // Nested tag - increase depth
                                this._elementDepth++;
                            }
                        }

                        this._inTag = false;
                    }
                }
                else
                {
                    // Not in a tag - copy directly to output (convert char to byte)
                    var charBytes = Encoding.UTF8.GetBytes(new[] { currentChar });
                    foreach (var b in charBytes)
                    {
                        if (bufferPos >= count)
                        {
                            break;
                        }

                        buffer[bufferPos + offset] = b;
                        bufferPos++;
                        bytesRead++;
                    }

                    this._parentParser.Position++;
                    this._bytesRead++;
                }
            }

            return bytesRead;
        }

        /// <summary>
        /// Gets the number of bytes read from the parent parser
        /// </summary>
        public int BytesRead => this._bytesRead;

        /// <summary>
        /// Gets the final buffer position after reading foreign element content
        /// </summary>
        public int GetFinalPosition() => this._finalPosition;

        /// <summary>
        /// Gets the final total bytes read after reading foreign element content
        /// </summary>
        public int GetFinalTotalCharsRead() => this._finalTotalCharsRead;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// Parses HTML from a stream.
    /// </summary>
    /// <param name="stream">The stream to parse from</param>
    /// <param name="leaveOpen">true to leave the stream open after parsing completes; otherwise, false (default false). The stream will be closed when parsing completes (or if an exception occurs) unless leaveOpen is true.</param>
    /// <param name="nodeCreatedCallback">Optional callback invoked for each node created during parsing</param>
    /// <returns>The parsed HTML document</returns>
    public static DocumentNode Parse(Stream stream, bool leaveOpen = false, NodeCreatedCallback? nodeCreatedCallback = null)
    {
        using var parser = new HtmlParser(stream, leaveOpen: leaveOpen);
        return parser.Parse(nodeCreatedCallback);
    }

    /// <summary>
    /// Parses HTML from a byte array
    /// </summary>
    /// <param name="bytes">The byte array containing HTML</param>
    /// <param name="nodeCreatedCallback">Optional callback invoked for each node created during parsing</param>
    public static DocumentNode Parse(byte[] bytes, NodeCreatedCallback? nodeCreatedCallback = null)
    {
        using var stream = new MemoryStream(bytes);
        return Parse(stream, nodeCreatedCallback: nodeCreatedCallback);
    }

    /// <summary>
    /// Parses HTML from a string
    /// </summary>
    /// <param name="html">The HTML string to parse</param>
    /// <param name="nodeCreatedCallback">Optional callback invoked for each node created during parsing</param>
    public static DocumentNode Parse(string html, NodeCreatedCallback? nodeCreatedCallback = null)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        return Parse(bytes, nodeCreatedCallback);
    }
}