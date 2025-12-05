using System.Text;
using Femur.Parsing;
using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;

namespace Femur.Xml.Parser;

/// <summary>
/// Streaming XML parser that reads from a Stream and builds an AST of nodes.
/// XML is stricter than HTML - all tags must be closed, attributes must be quoted, case-sensitive.
/// 
/// PARSING STRATEGY:
/// - Uses a sliding buffer to read stream in chunks (default 4KB)
/// - Tracks absolute position across buffer boundaries for location tracking
/// - Maintains element stack to match opening/closing tags
/// - Processes tokens in a single pass (no separate tokenization phase)
/// - XML-specific: Handles processing instructions, namespaces, strict tag matching
/// </summary>
public class XmlParser : StreamParser<XmlDocumentNode>
{
    private XmlDocumentNode? _document;
    private ContainerNode? _currentParent;
    private Stack<XmlElementNode>? _elementStack;

    /// <summary>
    /// Creates a new XML parser for the given stream
    /// </summary>
    /// <param name="stream">The stream to parse</param>
    /// <param name="bufferSize">Size of the buffer for reading chunks (default 4096)</param>
    /// <param name="leaveOpen">true to leave the stream open after the parser is disposed; otherwise, false (default false)</param>
    public XmlParser(Stream stream, int bufferSize = 4096, bool leaveOpen = false) : base(stream, bufferSize, leaveOpen)
    {
    }

    /// <summary>
    /// Creates a new document instance
    /// </summary>
    protected override XmlDocumentNode CreateDocument()
    {
        return new XmlDocumentNode();
    }

    /// <summary>
    /// Initializes parsing state (stacks, flags, etc.)
    /// </summary>
    protected override void InitializeParsing(XmlDocumentNode document)
    {
        this._document = document;
        this._currentParent = document;
        this._elementStack = new Stack<XmlElementNode>();
    }

    /// <summary>
    /// Processes a single character from the stream
    /// </summary>
    protected override void ProcessCharacter(char ch, XmlDocumentNode document)
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
    /// Processes a tag (opening, closing, or special tag like comment/CDATA/processing instruction)
    /// 
    /// After detecting '&lt;', we examine the next character to determine tag type:
    /// - '!' - Special tag (comment, CDATA)
    /// - '?' - Processing instruction (&lt;?xml version="1.0"?&gt;)
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

        var nextCh = this.Buffer[this.Position];

        // Check for special tags
        if (nextCh == '!')
        {
            this.ProcessSpecialTag();
        }
        // Check for processing instructions (<?xml version="1.0"?>)
        else if (nextCh == '?')
        {
            this.ProcessProcessingInstruction();
        }
        // Check for closing tags
        else if (nextCh == '/')
        {
            this.ProcessClosingTag();
        }
        // Otherwise it's an opening tag
        else
        {
            this.ProcessOpeningTag();
        }
    }

    /// <summary>
    /// Processes special tags: comments (<!-- -->) and CDATA (<![CDATA[...]]>)
    /// </summary>
    private void ProcessSpecialTag()
    {
        this.Position++; // Skip '!'
        if (!this.ReadMore())
        {
            return;
        }

        var startPos = this.GetAbsolutePosition() - 2; // Include '<!' in position

        // Check for comment: <!--
        if (this.Position < this.Length && this.Buffer[this.Position] == '-' && this.Position + 1 < this.Length && this.Buffer[this.Position + 1] == '-')
        {
            this.ParseCommentTag(startPos);
        }
        // Check for CDATA: <![CDATA[
        else if (this.Position + 6 < this.Length)
        {
            var cdataStart = new string([
                this.Buffer[this.Position],
                this.Buffer[this.Position + 1],
                this.Buffer[this.Position + 2],
                this.Buffer[this.Position + 3],
                this.Buffer[this.Position + 4],
                this.Buffer[this.Position + 5],
                this.Buffer[this.Position + 6]
            ]);
            if (cdataStart == "[CDATA[")
            {
                this.ParseCDataTag(startPos);
            }
        }
    }

    /// <summary>
    /// Parses a comment tag: &lt;!-- comment --&gt;
    /// 
    /// Format: <!-- content -->
    /// Reads content until finding the closing '-->' marker.
    /// </summary>
    private void ParseCommentTag(int startPos)
    {
        this.Position += 2; // Skip '--'
        _ = this.StringBuilder.Clear();
        // Read until we find '-->'
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];
            if (ch == '-' && this.Position + 1 < this.Length && this.Buffer[this.Position + 1] == '-' && this.Position + 2 < this.Length && this.Buffer[this.Position + 2] == '>')
            {
                this.Position += 3; // Skip '-->'
                break;
            }

            _ = this.StringBuilder.Append(ch);
            this.Position++;
        }

        var content = this.StringBuilder.ToString();

        var comment = new CommentNode
        {
            Content = content,
            Location = new SourceLocation(startPos, this.GetAbsolutePosition() - startPos)
        };
        comment.SetParent(this._currentParent);
        this.AddChildWithSiblings(comment, this._currentParent);
        this.OnNodeCreated(comment);
    }

    /// <summary>
    /// Parses a CDATA tag: &lt;![CDATA[...]]&gt;
    /// 
    /// Format: &lt;![CDATA[ content ]]&gt;
    /// Handles closing by matching two ']' followed by '&gt;'.
    /// </summary>
    private void ParseCDataTag(int startPos)
    {
        this.Position += 7; // Skip '[CDATA['
        _ = this.StringBuilder.Clear();
        // Read until we find ']]>'
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];
            if (ch == ']' && this.Position + 1 < this.Length && this.Buffer[this.Position + 1] == ']' && this.Position + 2 < this.Length && this.Buffer[this.Position + 2] == '>')
            {
                this.Position += 3; // Skip ']]>'
                break;
            }

            _ = this.StringBuilder.Append(ch);
            this.Position++;
        }

        var content = this.StringBuilder.ToString();

        var cdata = new CDataNode
        {
            Content = content,
            Location = new SourceLocation(startPos, this.GetAbsolutePosition() - startPos)
        };
        cdata.SetParent(this._currentParent);
        this.AddChildWithSiblings(cdata, this._currentParent);
        this.OnNodeCreated(cdata);
    }

    /// <summary>
    /// Processes a processing instruction: <?target data?>
    /// XML-specific feature.
    /// </summary>
    private void ProcessProcessingInstruction()
    {
        this.Position++; // Skip '?'
        if (!this.ReadMore())
        {
            return;
        }

        var startPos = this.GetAbsolutePosition() - 2; // Include '<?' in position

        // Read target name (until whitespace or '?')
        var target = this.ReadUntilAny([' ', '\t', '\r', '\n', '?'], out _);

        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        // Read content until we find '?>'
        _ = this.StringBuilder.Clear();
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];
            if (ch == '?' && this.Position + 1 < this.Length && this.Buffer[this.Position + 1] == '>')
            {
                this.Position += 2; // Skip '?>'
                break;
            }

            _ = this.StringBuilder.Append(ch);
            this.Position++;
        }

        var pi = new ProcessingInstructionNode
        {
            Target = target,
            Content = this.StringBuilder.ToString().Trim(),
            Location = new SourceLocation(startPos, this.GetAbsolutePosition() - startPos)
        };
        pi.SetParent(this._currentParent);
        this.AddChildWithSiblings(pi, this._currentParent);
        this.OnNodeCreated(pi);

        // If this is the XML declaration (<?xml ...?>), store it in the document
        if (target.Equals("xml", StringComparison.OrdinalIgnoreCase) && this._document != null)
        {
            this._document.XmlDeclaration = pi;
        }
    }

    /// <summary>
    /// Processes a closing tag: &lt;/tag&gt;
    /// </summary>
    private void ProcessClosingTag()
    {
        this.Position++; // Skip '/'
        if (!this.ReadMore())
        {
            return;
        }

        // Read tag name until '>'
        var tagName = this.ReadUntil('>', includeStopChar: false);
        if (string.IsNullOrEmpty(tagName))
        {
            return;
        }

        tagName = tagName.Trim();

        // XML is case-sensitive, so we match exactly
        // Find matching opening tag in stack
        var foundMatch = false;
        XmlElementNode? matchedElement = null;

        while (this._elementStack!.Count > 0)
        {
            var element = this._elementStack.Pop();
            // XML is case-sensitive - exact match required
            if (element.TagName == tagName)
            {
                foundMatch = true;
                matchedElement = element;
                break;
            }
        }

        // Update current parent to the matched element's parent
        // This ensures subsequent sibling elements are added to the correct parent
        if (foundMatch && matchedElement != null)
        {
            this._currentParent = matchedElement.GetParent() as ContainerNode;
        }
        // If we didn't find a match and stack is empty, restore to document root
        if (!foundMatch && this._elementStack.Count == 0)
        {
            this._currentParent = this._document;
        }

        // Defensive check: ensure _currentParent is never null
        this._currentParent ??= this._document;
    }

    /// <summary>
    /// Processes an opening tag and updates the parent stack
    /// </summary>
    private void ProcessOpeningTag()
    {
        var startPos = this.GetAbsolutePosition() - 1; // -1 to include '<'

        var element = this.ParseOpeningTag(startPos);
        if (element == null)
        {
            return;
        }

        element.SetParent(this._currentParent);
        this.AddChildWithSiblings(element, this._currentParent);
        this.OnNodeCreated(element);

        // XML doesn't have void elements - all tags must be closed
        // Self-closing tags (<tag />) don't go on the stack
        if (!element.IsSelfClosing)
        {
            this._elementStack!.Push(element);
            this._currentParent = element;
        }
    }

    /// <summary>
    /// Processes text content until a tag is encountered
    /// </summary>
    private void ProcessTextContent()
    {
        var startPos = this.GetAbsolutePosition();
        var text = this.ParseText();

        // XML preserves all whitespace (unlike HTML which filters whitespace-only nodes)
        // However, we can still filter pure whitespace if desired for efficiency
        var trimmed = text.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        var endPos = this.GetAbsolutePosition();
        var textNode = new TextNode
        {
            Content = text,
            Location = new SourceLocation(startPos, endPos - startPos)
        };
        textNode.SetParent(this._currentParent);
        this.AddChildWithSiblings(textNode, this._currentParent);
        this.OnNodeCreated(textNode);
    }

    /// <summary>
    /// Parses an opening XML tag: <tag attr="value" />
    /// 
    /// XML differences from HTML:
    /// - Case-sensitive tag names
    /// - Attributes must be quoted (single or double quotes)
    /// - No void elements
    /// - Supports namespaces (prefix:element)
    /// </summary>
    private XmlElementNode? ParseOpeningTag(int startPos)
    {
        if (!this.ReadMore())
        {
            return null;
        }

        // Read tag name until we hit whitespace, '/', or '>'
        // XML tag names can include colons for namespaces (prefix:localname)
        var tagName = this.ReadUntilAny([' ', '\t', '\r', '\n', '/', '>'], out var delimiter);
        if (string.IsNullOrEmpty(tagName))
        {
            return null;
        }

        var element = new XmlElementNode { TagName = tagName };

        // Parse namespace prefix if present (prefix:localname)
        var colonIndex = tagName.IndexOf(':');
        if (colonIndex > 0 && colonIndex < tagName.Length - 1)
        {
            element.NamespacePrefix = tagName.Substring(0, colonIndex);
        }

        // Handle self-closing tags: <tag /> (but only if immediately after tag name)
        // If delimiter is '/', check if it's followed by '>' (self-closing) or if there are attributes first
        if (delimiter == '/')
        {
            // Check if this is immediately self-closing (<tag/>) or has attributes (<tag attr="value" />)
            this.SkipWhitespace();
            if (this.Position < this.Length && this.Buffer[this.Position] == '>')
            {
                // Immediate self-closing: <tag/>
                element.IsSelfClosing = true;
                this.Position++; // Skip '>'
                var endPos = this.GetAbsolutePosition();
                element.Location = new SourceLocation(startPos, endPos - startPos);
                return element;
            }
            // Otherwise, there might be attributes before the '/', so continue parsing
        }

        // Handle tags without attributes: <tag>
        if (delimiter == '>')
        {
            var endPos = this.GetAbsolutePosition();
            element.Location = new SourceLocation(startPos, endPos - startPos);
            return element;
        }

        // Parse attributes
        // XML requires attributes to be quoted (single or double quotes)
        this.SkipWhitespace();
        while (this.Position < this.Length || this.ReadMore())
        {
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

            // Self-closing tag
            if (ch == '/' && this.Position + 1 < this.Length && this.Buffer[this.Position + 1] == '>')
            {
                element.IsSelfClosing = true;
                this.Position += 2; // Skip '/>'
                break;
            }

            // Skip whitespace before attribute name
            this.SkipWhitespace();
            if (this.Position >= this.Length)
            {
                break;
            }

            // Parse attribute name
            var attrName = this.ReadUntilAny(['=', ' ', '\t', '\r', '\n', '/', '>'], out var attrDelimiter);
            if (string.IsNullOrEmpty(attrName))
            {
                this.Position++;
                continue;
            }

            attrName = attrName.Trim();
            if (string.IsNullOrEmpty(attrName))
            {
                // If delimiter was '=', we still need to skip it
                if (attrDelimiter == '=')
                {
                    this.Position++;
                }

                this.Position++;
                continue;
            }

            // Parse attribute value
            var attrValue = string.Empty;
            if (attrDelimiter == '=')
            {
                // ReadUntilAny already consumed '=', so we're positioned right after it
                this.SkipWhitespace();

                if (this.Position < this.Length || this.ReadMore())
                {
                    if (this.Position < this.Length)
                    {
                        var quoteChar = this.Buffer[this.Position];
                        // XML requires quotes (single or double)
                        if (quoteChar == '"' || quoteChar == '\'')
                        {
                            this.Position++; // Skip opening quote
                            // Read attribute value handling escaped quotes
                            _ = this.StringBuilder.Clear();
                            while (this.Position < this.Length || this.ReadMore())
                            {
                                if (this.Position >= this.Length)
                                {
                                    break;
                                }

                                var attrCh = this.Buffer[this.Position];

                                // Check for escaped quote (\")
                                if (attrCh == '\\')
                                {
                                    // Ensure we have the next character
                                    if (this.Position + 1 >= this.Length && !this.ReadMore())
                                    {
                                        break;
                                    }

                                    if (this.Position + 1 < this.Length)
                                    {
                                        var nextCh = this.Buffer[this.Position + 1];
                                        if (nextCh == quoteChar)
                                        {
                                            // Escaped quote - include the quote character, skip the backslash
                                            _ = this.StringBuilder.Append(quoteChar);
                                            this.Position += 2;
                                            continue;
                                        }
                                    }
                                }

                                // Check for closing quote
                                if (attrCh == quoteChar)
                                {
                                    this.Position++; // Skip closing quote
                                    break;
                                }

                                _ = this.StringBuilder.Append(attrCh);
                                this.Position++;
                            }

                            attrValue = this.StringBuilder.ToString();
                        }
                    }
                }
            }

            element.Attributes[attrName] = attrValue;

            // Handle namespace declarations (xmlns:prefix="uri" or xmlns="uri")
            if (attrName == "xmlns")
            {
                element.NamespaceUri = attrValue;
            }
            else if (attrName.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                // Store namespace URI for this prefix
                // In a full implementation, we'd maintain a namespace context
                _ = attrName.Substring(6); // Skip "xmlns:" - prefix extracted but not yet used
            }

            this.SkipWhitespace();
        }

        var finalEndPos = this.GetAbsolutePosition();
        element.Location = new SourceLocation(startPos, finalEndPos - startPos);
        return element;
    }

    /// <summary>
    /// Parses text content until a tag is encountered
    /// </summary>
    private string ParseText()
    {
        _ = this.StringBuilder.Clear();
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];
            if (ch == '<')
            {
                // Found start of next tag - stop here
                break;
            }

            _ = this.StringBuilder.Append(ch);
            this.Position++;
        }

        return this.StringBuilder.ToString();
    }

    /// <summary>
    /// Parses XML from a stream.
    /// </summary>
    /// <param name="stream">The stream to parse from</param>
    /// <param name="leaveOpen">true to leave the stream open after parsing completes; otherwise, false (default false). The stream will be closed when parsing completes (or if an exception occurs) unless leaveOpen is true.</param>
    /// <param name="nodeCreatedCallback">Optional callback invoked for each node created during parsing</param>
    /// <returns>The parsed XML document</returns>
    public static XmlDocumentNode Parse(Stream stream, bool leaveOpen = false, NodeCreatedCallback? nodeCreatedCallback = null)
    {
        using var parser = new XmlParser(stream, leaveOpen: leaveOpen);
        return parser.Parse(nodeCreatedCallback);
    }

    /// <summary>
    /// Parses XML from a byte array
    /// </summary>
    /// <param name="bytes">The byte array containing XML</param>
    /// <param name="nodeCreatedCallback">Optional callback invoked for each node created during parsing</param>
    public static XmlDocumentNode Parse(byte[] bytes, NodeCreatedCallback? nodeCreatedCallback = null)
    {
        using var stream = new MemoryStream(bytes);
        return Parse(stream, nodeCreatedCallback: nodeCreatedCallback);
    }

    /// <summary>
    /// Parses XML from a string
    /// </summary>
    /// <param name="xml">The XML string to parse</param>
    /// <param name="nodeCreatedCallback">Optional callback invoked for each node created during parsing</param>
    public static XmlDocumentNode Parse(string xml, NodeCreatedCallback? nodeCreatedCallback = null)
    {
        var bytes = Encoding.UTF8.GetBytes(xml);
        return Parse(bytes, nodeCreatedCallback);
    }
}