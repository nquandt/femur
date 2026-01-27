using System.Text;
using Femur.Parsing;
using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;
using YamlDotNet.RepresentationModel;

namespace Femur.Chtml.Parser;

/// <summary>
/// Streaming CHTML parser that reads from a Stream and builds an AST of nodes.
/// Supports optional YAML front matter delimited by ---
/// 
/// PARSING STRATEGY:
/// - Uses a sliding buffer to read stream in chunks (default 4KB)
/// - Tracks absolute position across buffer boundaries for location tracking
/// - Maintains element stack to match opening/closing tags
/// - Processes tokens in a single pass (no separate tokenization phase)
/// </summary>
public class ChtmlParser : StreamParser<ChtmlDocumentNode>
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
    private Stack<Node>? _directiveStack;
    private bool _isInsideScriptOrStyle;
    private Stack<Node>? _scriptStyleStack; // Stack to track ScriptNode/StyleNode for matching closing tags

    /// <summary>
    /// Creates a new CHTML parser for the given stream
    /// </summary>
    /// <param name="stream">The stream to parse</param>
    /// <param name="bufferSize">Size of the buffer for reading chunks (default 4096)</param>
    /// <param name="leaveOpen">true to leave the stream open after the parser is disposed; otherwise, false (default false)</param>
    public ChtmlParser(Stream stream, int bufferSize = 4096, bool leaveOpen = false) : base(stream, bufferSize, leaveOpen)
    {
    }

    /// <summary>
    /// Creates a new document instance
    /// </summary>
    protected override ChtmlDocumentNode CreateDocument()
    {
        return new ChtmlDocumentNode();
    }

    /// <summary>
    /// Initializes parsing state (stacks, flags, etc.)
    /// CHTML-specific: Also parses front matter if present
    /// </summary>
    protected override void InitializeParsing(ChtmlDocumentNode document)
    {
        // Store document reference
        this._document = document;

        // Parse front matter if present (must be at start of document)
        // This will consume the front matter section and position us at HTML content
        this.ParseFrontMatter(document);

        // Track current parent node for adding children
        // Start with document as root parent
        this._currentParent = document;

        // Stack to track opening tags for matching with closing tags
        // Allows us to handle nested elements correctly
        this._elementStack = new Stack<ElementNode>();

        // Stack to track directive nodes (IfNode, ForNode) for matching closing directives
        this._directiveStack = new Stack<Node>();

        // Stack to track ScriptNode/StyleNode for matching closing tags
        this._scriptStyleStack = new Stack<Node>();

        // Track if we're inside script/style tags where code blocks shouldn't be parsed
        this._isInsideScriptOrStyle = false;
    }

    /// <summary>
    /// Processes a single character from the stream
    /// CHTML-specific: Handles '{' for code blocks in addition to '&lt;' for tags
    /// </summary>
    protected override void ProcessCharacter(char ch, ChtmlDocumentNode document)
    {
        // CHTML-specific: Handle code blocks with '{'
        // Code blocks start with '{' - but only if not inside script/style tags
        // EXCEPTION: Only CHTML directives ({#if}, {#for}) should be processed inside script/style tags
        // Regular code blocks ({expression}) should NOT be parsed inside script tags to preserve JavaScript syntax
        if (ch == '{')
        {
            if (!this._isInsideScriptOrStyle)
            {
                // Not inside script/style - process as code block
                this.ProcessCodeBlock();
            }
            else if (this.IsDirectiveAtPosition())
            {
                // Inside script/style and it's a directive - process it
                this.ProcessCodeBlock();
            }
            else
            {
                // Inside script/style and NOT a directive - treat as literal text (JavaScript syntax)
                this.ProcessTextContent();
            }

            return;
        }

        // Tags start with '<' - route to tag processing
        if (ch == '<')
        {
            this.ProcessTag();
        }
        // Everything else is text content until we hit a '<' or '{'
        else
        {
            this.ProcessTextContent();
        }
    }

    /// <summary>
    /// Processes a tag (opening, closing, component, or special tag like comment/CDATA)
    /// 
    /// After detecting '&lt;', we examine the next character to determine tag type:
    /// - '!' → Special tag (comment, CDATA, DOCTYPE)
    /// - '/' → Closing tag
    /// - ':' or 'C:' → Component tag (&lt;:ComponentName /&gt; or &lt;C:ComponentName /&gt;)
    /// - Otherwise → Opening tag
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
        // Component tags: <:ComponentName /> or <C:ComponentName />
        else if (nextChar == ':' || (nextChar == 'C' && this.Position + 1 < this.Length && this.Buffer[this.Position + 1] == ':'))
        {
            // Handle <C:ComponentName /> syntax
            if (nextChar == 'C')
            {
                this.Position++; // Skip 'C'
            }

            this.ProcessComponentTag();
        }
        // Opening tags: <tag> or <tag />
        else
        {
            this.ProcessOpeningTag();
        }
    }

    /// <summary>
    /// Processes a special tag (comment, CDATA, or DOCTYPE)
    /// These tags don't affect the element hierarchy, so _currentParent stays unchanged
    /// </summary>
    private void ProcessSpecialTag()
    {
        // Track start position including the '<' character
        var startPos = this.GetAbsolutePosition() - 1; // -1 to include '<'

        // Advance past '!' character
        this.Position++;

        // Parse the special tag (handles comment, CDATA, or DOCTYPE)
        var node = this.ParseSpecialTag(startPos);

        // Add to current parent's children (doesn't change _currentParent)
        if (node != null)
        {
            node.SetParent(this._currentParent);
            this._currentParent?.Children.Add(node);
            this.OnNodeCreated(node);
        }
    }

    /// <summary>
    /// Processes a closing tag and updates the parent stack
    /// 
    /// When we encounter &lt;/tag&gt; or &lt;/:ComponentName&gt;, we need to:
    /// 1. Parse the tag name (handles both regular and component closing tags)
    /// 2. Find the matching opening tag in the appropriate stack
    /// 3. Pop elements from stack until we find a match
    /// 4. Update _currentParent to the matched element's parent
    /// 
    /// Note: We handle mismatched closing tags gracefully by popping up the stack
    /// until we find a match (similar to how browsers handle malformed HTML)
    /// </summary>
    private void ProcessClosingTag()
    {
        // Advance past '/' character
        this.Position++;

        // Parse the tag name (handles both </tag> and </:ComponentName>)
        var closingTagName = this.ParseClosingTag();
        if (string.IsNullOrEmpty(closingTagName))
        {
            return;
        }

        // Check if this is a script or style closing tag
        var isScriptOrStyle = string.Equals(closingTagName, "script", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(closingTagName, "style", StringComparison.OrdinalIgnoreCase);

        if (isScriptOrStyle)
        {
            // Handle script/style closing tags from scriptStyleStack
            var foundMatch = false;
            Node? matchedNode = null;

            while (this._scriptStyleStack!.Count > 0)
            {
                var topNode = this._scriptStyleStack.Pop();

                // Check if tag name matches
                var matches = false;
                if (string.Equals(closingTagName, "script", StringComparison.OrdinalIgnoreCase) && topNode is ScriptNode)
                {
                    matches = true;
                }
                else if (string.Equals(closingTagName, "style", StringComparison.OrdinalIgnoreCase) && topNode is StyleNode)
                {
                    matches = true;
                }

                if (matches)
                {
                    matchedNode = topNode;
                    foundMatch = true;
                    break;
                }

                // Tag doesn't match - continue up the stack
                this._currentParent = topNode.GetParent() as ContainerNode;
            }

            if (foundMatch && matchedNode != null)
            {
                this._currentParent = matchedNode.GetParent() as ContainerNode;
                this._isInsideScriptOrStyle = false;

                // Extract content from children
                if (matchedNode is ScriptNode scriptNode)
                {
                    scriptNode.Content = this.ExtractContentFromChildren(scriptNode).Trim();
                    // Check if this is a bottom script (top-level and at the bottom)
                    var isTopLevel = scriptNode.GetParent() is DocumentNode;
                    scriptNode.IsBottomScript = isTopLevel && this.IsScriptAtBottom();
                }
                else if (matchedNode is StyleNode styleNode)
                {
                    styleNode.Content = this.ExtractContentFromChildren(styleNode).Trim();
                    // Check if this is a bottom style (top-level and at the bottom)
                    var isTopLevel = styleNode.GetParent() is DocumentNode;
                    styleNode.IsBottomStyle = isTopLevel && this.IsStyleAtBottom();
                }
            }
            else if (this._scriptStyleStack.Count == 0)
            {
                // No match found and stack is empty, restore to document root
                this._currentParent = this._document;
            }
        }
        else
        {
            // Handle regular element closing tags from elementStack
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
                // We update _currentParent as we go up, but will override it if we find a match
                this._currentParent = topElement.GetParent() as ContainerNode;
            }

            // If we found a match, restore _currentParent to the matched element's parent
            if (foundMatch && matchedElement != null)
            {
                this._currentParent = matchedElement.GetParent() as ContainerNode;
            }
            // If we didn't find a match and stack is empty, restore to document root
            else if (!foundMatch && this._elementStack.Count == 0)
            {
                this._currentParent = this._document;
            }
        }

        // Defensive check: ensure _currentParent is never null
        this._currentParent ??= this._document;
    }

    /// <summary>
    /// Extracts content from script/style node children by reconstructing the original text.
    /// Handles TextNode, CodeNode, ForNode, IfNode, etc.
    /// </summary>
    private string ExtractContentFromChildren(ContainerNode node)
    {
        var sb = new StringBuilder();
        foreach (var child in node.Children)
        {
            this.ExtractContentFromNode(child, sb);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Recursively extracts content from a node and appends it to the StringBuilder.
    /// Handles TextNode, CodeNode, ForNode, IfNode, etc.
    /// </summary>
    private void ExtractContentFromNode(Node node, StringBuilder sb)
    {
        switch (node)
        {
            case TextNode textNode:
                sb.Append(textNode.Content);
                break;

            case CodeNode codeNode:
                // Code block: {expression} -> reconstruct as {expression}
                sb.Append('{');
                sb.Append(codeNode.Content);
                sb.Append('}');
                break;

            case ForNode forNode:
                // Directive: {#for var in collection}...{/for}
                sb.Append("{#for ");
                sb.Append(forNode.VariableName);
                sb.Append(" in ");
                sb.Append(forNode.CollectionExpression);
                sb.Append('}');
                // Reconstruct children
                foreach (var child in forNode.Children)
                {
                    this.ExtractContentFromNode(child, sb);
                }

                sb.Append("{/for}");
                break;

            case IfNode ifNode:
                // Directive: {#if condition}...{/if}
                sb.Append("{#if ");
                sb.Append(ifNode.Condition);
                sb.Append('}');
                // Reconstruct children
                foreach (var child in ifNode.Children)
                {
                    this.ExtractContentFromNode(child, sb);
                }

                sb.Append("{/if}");
                break;

            default:
                // For other node types, recursively process children
                if (node is ContainerNode container)
                {
                    foreach (var child in container.Children)
                    {
                        this.ExtractContentFromNode(child, sb);
                    }
                }

                break;
        }
    }

    /// <summary>
    /// Processes a component tag: &lt;:ComponentName /&gt;
    /// 
    /// Components are like elements but use the ':' prefix to indicate they're component references.
    /// They can be self-closing or have children, similar to regular elements.
    /// 
    /// Components don't affect the element stack (they're treated as leaf nodes in the HTML structure),
    /// but they can have children that get passed to the component for rendering.
    /// </summary>
    private void ProcessComponentTag()
    {
        // Track start position including the '<' character
        var startPos = this.GetAbsolutePosition() - 1; // -1 to include '<'

        // Advance past ':' character
        this.Position++;

        // Parse the component tag (name, attributes, self-closing indicator)
        var component = this.ParseComponentTag(startPos);
        if (component == null)
        {
            return;
        }

        // Set parent-child relationship
        component.SetParent(this._currentParent);
        this._currentParent?.Children.Add(component);
        this.OnNodeCreated(component);

        // If component has children (not self-closing), parse them
        // Component children are parsed until we find the closing tag </:ComponentName>
        if (!component.IsSelfClosing)
        {
            // Push component name to stack for matching closing tag
            // We use a dummy ElementNode just for stack tracking (component name matching)
            // Store the parent reference so we can restore it when closing
            var stackElement = new ElementNode
            {
                TagName = component.ComponentName,
            };
            stackElement.SetParent(this._currentParent); // Store parent for restoration

            this._elementStack!.Push(stackElement);
            this._currentParent = component; // New children will be added to this component
        }
    }

    /// <summary>
    /// Processes an opening tag and updates the parent stack
    /// 
    /// When we encounter &lt;tag&gt;, we:
    /// 1. Parse the tag name and attributes
    /// 2. Create an ElementNode, ScriptNode, or StyleNode and add it to current parent's children
    /// 3. If tag is not self-closing and not void, push it onto the stack
    /// 4. Update _currentParent to the new element (so children go here)
    /// 
    /// Void elements (br, img, etc.) and self-closing tags don't go on the stack
    /// because they can't have children and don't need matching closing tags
    /// </summary>
    private void ProcessOpeningTag()
    {
        // Track start position including the '<' character
        var startPos = this.GetAbsolutePosition() - 1; // -1 to include '<'

        // Peek ahead to check if this is a script or style tag
        var savedPosition = this.Position;
        var tagName = this.ReadUntilAny([' ', '\t', '\r', '\n', '/', '>'], out _);
        this.Position = savedPosition; // Restore position

        // Check if this is a script or style tag - create ScriptNode/StyleNode directly
        Node? node;
        if (string.Equals(tagName, "script", StringComparison.OrdinalIgnoreCase))
        {
            node = this.ParseScriptTag(startPos);
        }
        else if (string.Equals(tagName, "style", StringComparison.OrdinalIgnoreCase))
        {
            node = this.ParseStyleTag(startPos);
        }
        else
        {
            // Parse as regular ElementNode
            node = this.ParseOpeningTag(startPos);
        }

        if (node == null)
        {
            return;
        }

        // Set parent-child relationship
        node.SetParent(this._currentParent);
        this._currentParent?.Children.Add(node);
        this.OnNodeCreated(node);

        // Handle script/style nodes
        if (node is ScriptNode scriptNode)
        {
            if (!scriptNode.IsSelfClosing)
            {
                this._scriptStyleStack!.Push(scriptNode);
                this._currentParent = scriptNode;
                this._isInsideScriptOrStyle = true;
            }
        }
        else if (node is StyleNode styleNode)
        {
            if (!styleNode.IsSelfClosing)
            {
                this._scriptStyleStack!.Push(styleNode);
                this._currentParent = styleNode;
                this._isInsideScriptOrStyle = true;
            }
        }
        // Handle regular elements
        else if (node is ElementNode element)
        {
            // Only push non-void, non-self-closing elements onto stack
            // These elements can have children and need matching closing tags
            if (!element.IsSelfClosing && !element.IsVoidElement)
            {
                this._elementStack!.Push(element);
                this._currentParent = element; // New children will be added to this element
            }
        }
    }

    /// <summary>
    /// Checks if the current position contains a directive ({#if}, {#for}, {/if}, {/for}).
    /// This is used to allow directives even inside script/style tags.
    /// </summary>
    private bool IsDirectiveAtPosition()
    {
        // We're positioned at '{', check the next character
        if (this.Position + 1 >= this.Length && !this.ReadMore())
        {
            return false;
        }

        var nextChar = this.Buffer[this.Position + 1];

        // Opening directive: {#if}, {#for}
        if (nextChar == '#')
        {
            return true;
        }

        // Closing directive: {/if}, {/for}
        if (nextChar == '/')
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Processes text content until a tag is encountered
    /// 
    /// Text content is everything between tags. We:
    /// 1. Parse all characters until we hit '&lt;' (start of next tag)
    /// 2. If inside script/style tags, treat '{' as literal (don't stop)
    /// 3. If NOT inside script/style tags, stop at '{' (code block)
    /// 4. Filter out pure whitespace text nodes (but keep the content if it has any non-whitespace)
    /// 5. Create TextNode with location tracking
    /// 
    /// Note: We preserve whitespace in the content but only create nodes for text with actual content
    /// </summary>
    private void ProcessTextContent()
    {
        // Track where text starts for location information
        var startPos = this.GetAbsolutePosition();

        // Read all characters until we hit '<' (which starts the next tag)
        // If inside script/style, also read '{' as literal text
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
        this._currentParent?.Children.Add(textNode);
        this.OnNodeCreated(textNode);
    }

    /// <summary>
    /// Processes a code block: { ... }
    /// 
    /// Code blocks can be:
    /// - Regular expressions: {props.Title}
    /// - Directives: {#if condition}...{/if}, {#for item in collection}...{/for}
    /// 
    /// Directives are handled specially with proper nesting support.
    /// </summary>
    private void ProcessCodeBlock()
    {
        // Track start position including the opening '{'
        var startPos = this.GetAbsolutePosition();

        // Advance past opening '{'
        this.Position++;

        if (!this.ReadMore())
        {
            return;
        }

        // Check if this is a directive by peeking at the next character
        var nextChar = this.Buffer[this.Position];

        // Check for closing directives: {/if}, {/for}
        if (nextChar == '/')
        {
            this.ProcessClosingDirective(startPos);
            return;
        }

        // Check for opening directives: {#if}, {#for}
        if (nextChar == '#')
        {
            // Try to parse as directive - returns true if successful
            if (!this.ProcessOpeningDirective(startPos))
            {
                // Directive parsing failed - parse as regular code block instead
                // Reset position to before the '#' so ParseCodeBlock can read it
                this.Position = startPos + 1; // After the '{'
                var fallbackCodeNode = this.ParseCodeBlock(startPos);
                if (fallbackCodeNode != null)
                {
                    fallbackCodeNode.SetParent(this._currentParent);
                    this._currentParent?.Children.Add(fallbackCodeNode);
                    this.OnNodeCreated(fallbackCodeNode);
                }
            }

            return;
        }

        // Regular code block - parse as expression
        var codeNode = this.ParseCodeBlock(startPos);

        // Add to current parent's children (doesn't change _currentParent)
        if (codeNode != null)
        {
            codeNode.SetParent(this._currentParent);
            this._currentParent?.Children.Add(codeNode);
            this.OnNodeCreated(codeNode);
        }
    }

    /// <summary>
    /// Processes an opening directive: {#if condition} or {#for item in collection}
    /// Returns true if a directive node was successfully created, false otherwise.
    /// </summary>
    private bool ProcessOpeningDirective(int startPos)
    {
        // Advance past '#'
        this.Position++;

        if (!this.ReadMore())
        {
            return false;
        }

        // Read directive name (if, for, etc.)
        var directiveName = this.ReadUntilAny([' ', '\t', '\r', '\n', '}'], out _);

        if (string.IsNullOrEmpty(directiveName))
        {
            return false;
        }

        Node? directiveNode;
        if (directiveName.Equals("if", StringComparison.OrdinalIgnoreCase))
        {
            // Parse condition expression
            this.SkipWhitespace();
            var condition = this.ReadUntil('}');
            condition = condition.Trim();

            var ifNode = new IfNode
            {
                Condition = condition,
                Location = new SourceLocation(startPos, this.GetAbsolutePosition() - startPos)
            };

            directiveNode = ifNode;
        }
        else if (directiveName.Equals("for", StringComparison.OrdinalIgnoreCase))
        {
            // Parse: variableName in collectionExpression
            // Skip whitespace after "for"
            this.SkipWhitespace();

            // Read variable name (everything until next whitespace or '}')
            var variableName = this.ReadUntilAny([' ', '\t', '\r', '\n', '}'], out var varDelimiter);
            variableName = variableName.Trim();

            if (string.IsNullOrEmpty(variableName) || varDelimiter == '}')
            {
                // Invalid syntax - return false to indicate failure
                return false;
            }

            // Skip whitespace and check for "in" keyword
            this.SkipWhitespace();
            var inKeyword = this.ReadUntilAny([' ', '\t', '\r', '\n', '}'], out var inDelimiter);
            inKeyword = inKeyword.Trim();

            if (!inKeyword.Equals("in", StringComparison.OrdinalIgnoreCase) || inDelimiter == '}')
            {
                // Invalid syntax - return false to indicate failure
                return false;
            }

            // Read collection expression (everything until '}')
            this.SkipWhitespace();
            var collectionExpression = this.ReadUntil('}');
            collectionExpression = collectionExpression.Trim();

            if (string.IsNullOrEmpty(collectionExpression))
            {
                // Invalid syntax - return false to indicate failure
                return false;
            }

            var forNode = new ForNode
            {
                VariableName = variableName,
                CollectionExpression = collectionExpression,
                Location = new SourceLocation(startPos, this.GetAbsolutePosition() - startPos)
            };

            directiveNode = forNode;
        }
        else
        {
            // Unknown directive - return false to indicate failure
            return false;
        }

        if (directiveNode != null)
        {
            // Set parent and add to current parent's children
            directiveNode.SetParent(this._currentParent);
            this._currentParent?.Children.Add(directiveNode);
            this.OnNodeCreated(directiveNode);

            // Push to directive stack for matching closing directive
            this._directiveStack!.Push(directiveNode);

            // Update _currentParent so children go into the directive node
            this._currentParent = directiveNode as ContainerNode;

            return true; // Success
        }

        return false; // Should not reach here, but return false for safety
    }

    /// <summary>
    /// Processes a closing directive: {/if} or {/for}
    /// </summary>
    private void ProcessClosingDirective(int startPos)
    {
        // Advance past '/'
        this.Position++;

        if (!this.ReadMore())
        {
            return;
        }

        // Read directive name (if, for, etc.) - stop at whitespace or '}'
        var directiveName = this.ReadUntilAny([' ', '\t', '\r', '\n', '}'], out var delimiter);
        directiveName = directiveName.Trim();

        // If we stopped at '}', we're done (directive name was immediately followed by '}')
        if (delimiter == '}')
        {
            // Already consumed the '}' - we're done, proceed to matching
        }
        else
        {
            // Otherwise, skip whitespace until we find the closing brace
            this.SkipWhitespace();

            // Now find and consume the closing '}'
            while (this.Position < this.Length || this.ReadMore())
            {
                if (this.Position >= this.Length)
                {
                    break;
                }

                if (this.Buffer[this.Position] == '}')
                {
                    this.Position++;
                    break;
                }

                this.Position++;
            }
        }

        // Find matching opening directive in stack
        Node? matchedDirective = null;

        while (this._directiveStack!.Count > 0)
        {
            var topDirective = this._directiveStack.Pop();

            // Check if directive types match
            var matches = false;
            if (directiveName.Equals("if", StringComparison.OrdinalIgnoreCase) && topDirective is IfNode)
            {
                matches = true;
            }
            else if (directiveName.Equals("for", StringComparison.OrdinalIgnoreCase) && topDirective is ForNode)
            {
                matches = true;
            }

            if (matches)
            {
                matchedDirective = topDirective;
                break;
            }

            // Mismatched directive - restore _currentParent and continue searching
            this._currentParent = topDirective.GetParent() as ContainerNode;
        }

        // If we found a match, restore _currentParent to the directive's parent
        if (matchedDirective != null)
        {
            this._currentParent = matchedDirective.GetParent() as ContainerNode;
        }
        // If no match found and stack is empty, restore to document root
        else if (this._directiveStack.Count == 0)
        {
            this._currentParent = this._document;
        }

        // Defensive check: ensure _currentParent is never null
        this._currentParent ??= this._document;
    }

    /// <summary>
    /// Parses a code block: { content }
    /// 
    /// Currently just captures all content between braces.
    /// Nested braces are NOT allowed - the first '}' closes the code block.
    /// Any '{' characters within the content are treated as literal content.
    /// 
    /// TODO: Future enhancement location - Parse code content here
    /// Replace the simple content capture with:
    /// - Tokenization of code content
    /// - Expression parsing
    /// - Syntax validation
    /// - Store parsed tokens/expressions in CodeNode
    /// </summary>
    private CodeNode? ParseCodeBlock(int startPos)
    {
        if (!this.ReadMore())
        {
            return null;
        }

        this.StringBuilder.Clear();

        // Read until we find the closing brace (first '}' closes the block)
        // Nested braces are NOT supported - any '{' inside is treated as literal content
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];
            this.Position++;

            if (ch == '}')
            {
                // Found closing brace - end of code block
                // Don't append '}' to content (it's not part of the code)
                break;
            }
            else
            {
                // Regular character or '{' - add to content
                // Note: '{' characters are treated as literal content (no nesting allowed)
                this.StringBuilder.Append(ch);
            }
        }

        // Create code node with captured content
        var content = this.StringBuilder.ToString();
        var endPos = this.GetAbsolutePosition();

        return new CodeNode
        {
            Content = content,
            Location = new SourceLocation(startPos, endPos - startPos)
        };
    }

    /// <summary>
    /// Parses CHTML from a stream.
    /// </summary>
    /// <param name="stream">The stream to parse from</param>
    /// <param name="leaveOpen">true to leave the stream open after parsing completes; otherwise, false (default false). The stream will be closed when parsing completes (or if an exception occurs) unless leaveOpen is true.</param>
    /// <param name="nodeCreatedCallback">Optional callback invoked for each node created during parsing</param>
    /// <returns>The parsed CHTML document</returns>
    public static ChtmlDocumentNode Parse(Stream stream, bool leaveOpen = false, NodeCreatedCallback? nodeCreatedCallback = null)
    {
        using var parser = new ChtmlParser(stream, leaveOpen: leaveOpen);
        return parser.Parse(nodeCreatedCallback);
    }

    /// <summary>
    /// Parses CHTML from a byte array
    /// </summary>
    /// <param name="bytes">The byte array containing CHTML</param>
    /// <param name="nodeCreatedCallback">Optional callback invoked for each node created during parsing</param>
    public static ChtmlDocumentNode Parse(byte[] bytes, NodeCreatedCallback? nodeCreatedCallback = null)
    {
        using var stream = new MemoryStream(bytes);
        return Parse(stream, nodeCreatedCallback: nodeCreatedCallback);
    }

    /// <summary>
    /// Parses CHTML from a string
    /// </summary>
    /// <param name="html">The CHTML string to parse</param>
    /// <param name="nodeCreatedCallback">Optional callback invoked for each node created during parsing</param>
    public static ChtmlDocumentNode Parse(string html, NodeCreatedCallback? nodeCreatedCallback = null)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        return Parse(bytes, nodeCreatedCallback);
    }

    /// <summary>
    /// Parses front matter if present. Returns the byte offset where front matter ends (0 if none found).
    /// Front matter is delimited by --- on separate lines at the start of the document.
    /// 
    /// PROCESSING:
    /// 1. Save current position in case front matter isn't found (need to reset)
    /// 2. Check if first line is exactly "---"
    /// 3. If yes, read lines until we find closing "---"
    /// 4. Parse YAML content and store in document
    /// 5. If parsing fails or delimiters missing, reset position and treat as no front matter
    /// </summary>
    private int ParseFrontMatter(ChtmlDocumentNode document)
    {
        // Read initial chunk to check for front matter
        if (!this.ReadMore())
        {
            return 0;
        }

        // Save current state so we can reset if front matter isn't present
        var startPos = this.GetAbsolutePosition();
        var savedPosition = this.Position;
        var savedLength = this.Length;
        var savedTotalCharsRead = this.TotalCharsRead;

        // Front matter MUST start with "---" on the first line
        var firstLine = this.ReadLine();
        if (firstLine == null || firstLine.Trim() != "---")
        {
            // No front matter found - reset to saved position and continue parsing HTML
            this.Position = savedPosition;
            this.Length = savedLength;
            this.TotalCharsRead = savedTotalCharsRead;
            return 0;
        }

        // Found opening delimiter - read front matter content line by line
        var frontMatterBuilder = new StringBuilder();
        string? line;
        var foundClosing = false;

        // Read lines until we find closing "---" delimiter
        while ((line = this.ReadLine()) != null)
        {
            if (line.Trim() == "---")
            {
                foundClosing = true;
                break;
            }

            frontMatterBuilder.AppendLine(line);
        }

        // If no closing delimiter found, treat as no front matter
        if (!foundClosing)
        {
            // Reset position - front matter was incomplete
            this.Position = savedPosition;
            this.Length = savedLength;
            this.TotalCharsRead = savedTotalCharsRead;
            return 0;
        }

        // Parse YAML front matter
        var frontMatterText = frontMatterBuilder.ToString();
        document.FrontMatterRaw = frontMatterText; // Store raw text even if parsing fails

        try
        {
            // Parse YAML into structured dictionary
            var yaml = new YamlStream();
            yaml.Load(new StringReader(frontMatterText));

            if (yaml.Documents.Count > 0 && yaml.Documents[0].RootNode is YamlMappingNode root)
            {
                document.FrontMatter = this.ParseYamlNode(root);
            }
        }
        catch
        {
            // If YAML parsing fails, just store the raw text
            // FrontMatter will remain null, but FrontMatterRaw will have the content
            // This allows consumers to handle malformed YAML if needed
        }

        return this.GetAbsolutePosition() - startPos;
    }

    /// <summary>
    /// Recursively parses a YAML node into a dictionary
    /// </summary>
    private Dictionary<string, object> ParseYamlNode(YamlMappingNode node)
    {
        var result = new Dictionary<string, object>();

        foreach (var entry in node.Children)
        {
            var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
            object value = entry.Value switch
            {
                YamlScalarNode scalar => scalar.Value ?? string.Empty,
                YamlMappingNode mapping => this.ParseYamlNode(mapping),
                YamlSequenceNode sequence => this.ParseYamlSequence(sequence),
                _ => entry.Value.ToString() ?? string.Empty
            };
            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// Parses a YAML sequence into a list
    /// </summary>
    private List<object> ParseYamlSequence(YamlSequenceNode sequence)
    {
        var result = new List<object>();

        foreach (var item in sequence.Children)
        {
            object value = item switch
            {
                YamlScalarNode scalar => scalar.Value ?? string.Empty,
                YamlMappingNode mapping => this.ParseYamlNode(mapping),
                YamlSequenceNode seq => this.ParseYamlSequence(seq),
                _ => item.ToString() ?? string.Empty
            };
            result.Add(value);
        }

        return result;
    }

    /// <summary>
    /// Reads a line from the current buffer position, handling multi-buffer reads
    /// 
    /// Handles both \n (Unix) and \r\n (Windows) line endings.
    /// Continues reading across buffer boundaries if needed.
    /// Returns null if end of stream is reached without finding a newline.
    /// </summary>
    private string? ReadLine()
    {
        this.StringBuilder.Clear();
        var foundNewline = false;

        // Read until we find a newline or exhaust the stream
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];
            this.Position++;

            // Handle Windows line endings (\r\n)
            if (ch == '\r')
            {
                // Check if next character is \n (Windows style)
                if (this.Position < this.Length && this.Buffer[this.Position] == '\n')
                {
                    this.Position++;
                }

                foundNewline = true;
                break;
            }
            // Handle Unix line endings (\n)
            else if (ch == '\n')
            {
                foundNewline = true;
                break;
            }
            else
            {
                // Accumulate character into line
                this.StringBuilder.Append(ch);
            }
        }

        // Return null if we hit end of stream without finding newline
        if (this.StringBuilder.Length == 0 && !foundNewline)
        {
            return null;
        }

        return this.StringBuilder.ToString();
    }

    /// <summary>
    /// Reads a code block in an attribute value: {expression}
    /// Includes the braces in the returned value.
    /// Properly handles quotes inside the code block (e.g., {props.X ?? "default"}).
    /// </summary>
    private string ReadCodeBlockInAttribute()
    {
        this.StringBuilder.Clear();
        // Note: This method expects to be called when positioned AT the opening '{'
        // It will read the '{' and all content until the matching '}'

        var braceDepth = 0; // Start at 0, will be incremented when we read the opening '{'
        var inString = false; // Track if we're inside a string literal
        var stringChar = '\0'; // Track which quote character started the string

        // Read until we find the matching closing brace
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];
            this.Position++;

            this.StringBuilder.Append(ch);

            if (inString)
            {
                // Inside a string literal - check for closing quote
                if (ch == stringChar)
                {
                    // Check if it's escaped (preceded by backslash)
                    if (this.Position > 1 && this.Buffer[this.Position - 2] == '\\')
                    {
                        // Escaped quote - continue
                        continue;
                    }
                    // Closing quote found - exit string mode
                    inString = false;
                    stringChar = '\0';
                }
            }
            else
            {
                // Not in a string - check for string start or brace
                if (ch == '"' || ch == '\'')
                {
                    // Start of string literal
                    inString = true;
                    stringChar = ch;
                }
                else if (ch == '{')
                {
                    braceDepth++;
                }
                else if (ch == '}')
                {
                    braceDepth--;
                    if (braceDepth == 0)
                    {
                        // Found matching closing brace - end of code block
                        break;
                    }
                }
            }
        }

        return this.StringBuilder.ToString();
    }

    /// <summary>
    /// Reads a component name that can include dots and relative paths.
    /// Supports:
    /// - Simple: ComponentName
    /// - Relative: .ComponentName or ./ComponentName
    /// - Fully qualified: Namespace.ComponentName
    /// Stops at whitespace, '/', or '>'
    /// </summary>
    private string ReadComponentName(out char matchedChar)
    {
        this.StringBuilder.Clear();
        matchedChar = '\0';
        var hasLeadingDot = false;

        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];

            // Special handling for relative paths: if we start with '.' and encounter '/', 
            // allow it as part of the component name (e.g., "./ComponentName")
            if (hasLeadingDot && ch == '/')
            {
                this.StringBuilder.Append(ch);
                this.Position++;
                hasLeadingDot = false; // Reset flag after consuming '/'
                continue;
            }

            // Stop at whitespace, '/', or '>'
            if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n' || ch == '/' || ch == '>')
            {
                matchedChar = ch;
                this.Position++;
                break;
            }

            // Allow letters, digits, dots, hyphens, underscores
            // This allows component names like: ComponentName, .ComponentName, ./ComponentName, Namespace.ComponentName
            if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_')
            {
                if (ch == '.' && this.StringBuilder.Length == 0)
                {
                    hasLeadingDot = true;
                }

                this.StringBuilder.Append(ch);
                this.Position++;
            }
            else
            {
                // Invalid character - stop reading
                matchedChar = ch;
                this.Position++;
                break;
            }
        }

        return this.StringBuilder.ToString();
    }

    /// <summary>
    /// Parses an opening HTML tag: <tag attr="value" />
    /// 
    /// PROCESSING FLOW:
    /// 1. Read tag name (until whitespace, '/', or '>')
    /// 2. Check if void element (br, img, etc.) - these can't have children
    /// 3. Handle self-closing tags (<tag /> or <tag/>)
    /// 4. Parse attributes if present:
    ///    - Attribute name until '=', whitespace, or '>'
    ///    - Attribute value (quoted or unquoted)
    ///    - Boolean attributes (no value)
    /// 5. Set location tracking for the element
    /// 
    /// ATTRIBUTE HANDLING:
    /// - Quoted values: "value" or 'value'
    /// - Unquoted values: value (until whitespace or '>')
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
            else if (ch == '{')
            {
                // Unquoted code block: attr={expression}
                // ReadCodeBlockInAttribute() expects to be positioned at the '{'
                // and will read it, so we don't need to advance here
                attrValue = this.ReadCodeBlockInAttribute();
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
    /// Parses a script tag: &lt;script attr="value" /&gt;
    /// 
    /// Similar to ParseOpeningTag but creates a ScriptNode instead of ElementNode.
    /// </summary>
    private ScriptNode? ParseScriptTag(int startPos)
    {
        if (!this.ReadMore())
        {
            return null;
        }

        // Read tag name until we hit whitespace, '/', or '>'
        var tagName = this.ReadUntilAny([' ', '\t', '\r', '\n', '/', '>'], out var delimiter);
        if (string.IsNullOrEmpty(tagName) || !string.Equals(tagName, "script", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var scriptNode = new ScriptNode();

        // Handle self-closing tags: <script />
        if (delimiter == '/')
        {
            scriptNode.IsSelfClosing = true;
            // Skip the '>' that follows '/'
            if (this.Position < this.Length && this.Buffer[this.Position] == '>')
            {
                this.Position++;
            }

            var endPos = this.GetAbsolutePosition();
            scriptNode.Location = new SourceLocation(startPos, endPos - startPos);
            return scriptNode;
        }

        // Handle tags without attributes: <script>
        if (delimiter == '>')
        {
            var endPos = this.GetAbsolutePosition();
            scriptNode.Location = new SourceLocation(startPos, endPos - startPos);
            return scriptNode;
        }

        // Parse attributes (same logic as regular elements)
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

            // Self-closing tag: <script attr="value" />
            if (ch == '/')
            {
                this.Position++;
                scriptNode.IsSelfClosing = true;
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

            // Boolean attribute without value: <script attr>
            if (attrDelimiter == '>')
            {
                scriptNode.Attributes[attrName] = string.Empty;
                break;
            }

            // Boolean attribute: <script attr >
            if (attrDelimiter != '=')
            {
                scriptNode.Attributes[attrName] = string.Empty;
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
            else if (ch == '{')
            {
                // Unquoted code block: attr={expression}
                attrValue = this.ReadCodeBlockInAttribute();
            }
            // Unquoted attribute value: attr=value
            else
            {
                attrValue = this.ReadUntilAny([' ', '\t', '\r', '\n', '/', '>'], out _);
            }

            scriptNode.Attributes[attrName] = attrValue;
        }

        var finalEndPos = this.GetAbsolutePosition();
        scriptNode.Location = new SourceLocation(startPos, finalEndPos - startPos);
        return scriptNode;
    }

    /// <summary>
    /// Parses a style tag: &lt;style attr="value" /&gt;
    /// 
    /// Similar to ParseOpeningTag but creates a StyleNode instead of ElementNode.
    /// </summary>
    private StyleNode? ParseStyleTag(int startPos)
    {
        if (!this.ReadMore())
        {
            return null;
        }

        // Read tag name until we hit whitespace, '/', or '>'
        var tagName = this.ReadUntilAny([' ', '\t', '\r', '\n', '/', '>'], out var delimiter);
        if (string.IsNullOrEmpty(tagName) || !string.Equals(tagName, "style", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var styleNode = new StyleNode();

        // Handle self-closing tags: <style />
        if (delimiter == '/')
        {
            styleNode.IsSelfClosing = true;
            // Skip the '>' that follows '/'
            if (this.Position < this.Length && this.Buffer[this.Position] == '>')
            {
                this.Position++;
            }

            var endPos = this.GetAbsolutePosition();
            styleNode.Location = new SourceLocation(startPos, endPos - startPos);
            return styleNode;
        }

        // Handle tags without attributes: <style>
        if (delimiter == '>')
        {
            var endPos = this.GetAbsolutePosition();
            styleNode.Location = new SourceLocation(startPos, endPos - startPos);
            return styleNode;
        }

        // Parse attributes (same logic as regular elements)
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

            // Self-closing tag: <style attr="value" />
            if (ch == '/')
            {
                this.Position++;
                styleNode.IsSelfClosing = true;
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

            // Boolean attribute without value: <style attr>
            if (attrDelimiter == '>')
            {
                styleNode.Attributes[attrName] = string.Empty;
                break;
            }

            // Boolean attribute: <style attr >
            if (attrDelimiter != '=')
            {
                styleNode.Attributes[attrName] = string.Empty;
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
            else if (ch == '{')
            {
                // Unquoted code block: attr={expression}
                attrValue = this.ReadCodeBlockInAttribute();
            }
            // Unquoted attribute value: attr=value
            else
            {
                attrValue = this.ReadUntilAny([' ', '\t', '\r', '\n', '/', '>'], out _);
            }

            styleNode.Attributes[attrName] = attrValue;
        }

        var finalEndPos = this.GetAbsolutePosition();
        styleNode.Location = new SourceLocation(startPos, finalEndPos - startPos);
        return styleNode;
    }

    /// <summary>
    /// Parses a component tag: &lt;:ComponentName attr="value" /&gt;
    /// 
    /// Similar to ParseOpeningTag but handles component syntax with ':' prefix.
    /// Components can have attributes and be self-closing or have children.
    /// 
    /// HANDLES:
    /// - Self-closing: &lt;:Header /&gt;
    /// - With attributes: &lt;:Layout title="Home" /&gt;
    /// - With children: &lt;:Layout&gt;...&lt;/:Layout&gt;
    /// </summary>
    private ComponentNode? ParseComponentTag(int startPos)
    {
        if (!this.ReadMore())
        {
            return null;
        }

        // Read component name - supports:
        // - Simple: ComponentName
        // - Relative: .ComponentName or ./ComponentName (same directory)
        // - Fully qualified: Namespace.ComponentName
        // Component names can include dots and start with dots
        var componentName = this.ReadComponentName(out var delimiter);
        if (string.IsNullOrEmpty(componentName))
        {
            return null;
        }

        var component = new ComponentNode { ComponentName = componentName };

        // Handle self-closing: <:ComponentName />
        if (delimiter == '/')
        {
            component.IsSelfClosing = true;
            if (this.Position < this.Length && this.Buffer[this.Position] == '>')
            {
                this.Position++;
            }

            var endPos = this.GetAbsolutePosition();
            component.Location = new SourceLocation(startPos, endPos - startPos);
            return component;
        }

        // Handle component without attributes: <:ComponentName>
        if (delimiter == '>')
        {
            var endPos = this.GetAbsolutePosition();
            component.Location = new SourceLocation(startPos, endPos - startPos);
            return component;
        }

        // Parse attributes (same logic as regular elements)
        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            this.SkipWhitespace();

            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];

            if (ch == '>')
            {
                this.Position++;
                break;
            }

            if (ch == '/')
            {
                this.Position++;
                component.IsSelfClosing = true;
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

            if (attrDelimiter == '>')
            {
                component.Attributes[attrName] = string.Empty;
                break;
            }

            if (attrDelimiter != '=')
            {
                component.Attributes[attrName] = string.Empty;
                continue;
            }

            // Skip whitespace after '='
            this.SkipWhitespace();

            if (this.Position >= this.Length)
            {
                break;
            }

            ch = this.Buffer[this.Position];
            string attrValue;

            if (ch == '"' || ch == '\'')
            {
                this.Position++;
                attrValue = this.ReadUntil(ch);
            }
            else if (ch == '{')
            {
                // Code block in attribute value: {expression}
                // Read the entire code block including braces
                attrValue = this.ReadCodeBlockInAttribute();
            }
            else
            {
                attrValue = this.ReadUntilAny([' ', '\t', '\r', '\n', '/', '>'], out _);
            }

            component.Attributes[attrName] = attrValue;
        }

        var finalEndPos = this.GetAbsolutePosition();
        component.Location = new SourceLocation(startPos, finalEndPos - startPos);
        return component;
    }

    /// <summary>
    /// Parses a closing tag: &lt;/tag&gt; or &lt;/:ComponentName&gt;
    /// 
    /// Reads the tag name and consumes everything until the closing '&gt;'.
    /// Returns the tag name for matching against the element stack.
    /// Handles both regular closing tags (&lt;/tag&gt;) and component closing tags (&lt;/:ComponentName&gt;).
    /// </summary>
    private string ParseClosingTag()
    {
        // Check if this is a component closing tag: </:ComponentName> or </C:ComponentName>
        if (this.Position < this.Length && this.Buffer[this.Position] == ':')
        {
            this.Position++; // Skip ':'
        }
        else if (this.Position < this.Length && this.Buffer[this.Position] == 'C' &&
                 this.Position + 1 < this.Length && this.Buffer[this.Position + 1] == ':')
        {
            this.Position += 2; // Skip 'C:'
        }

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
            // Comment: <!-- ... -->
            // Format: <!-- (dash dash) content (dash dash) >
            this.Position++; // Skip second '-'
            this.StringBuilder.Clear();
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
                        this.StringBuilder.Append(new string('-', dashCount - 1));
                        dashCount = 1;
                        continue;
                    }
                }
                else
                {
                    // Not a dash - flush any accumulated dashes and add character
                    if (dashCount > 0)
                    {
                        this.StringBuilder.Append(new string('-', dashCount));
                        dashCount = 0;
                    }

                    this.StringBuilder.Append(c);
                }

                this.Position++;
            }

            // Clean up trailing dashes if comment ended abruptly
            var content = this.StringBuilder.ToString();
            if (content.EndsWith("--"))
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
        else if (ch == '[')
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
        else
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
    }

    /// <summary>
    /// Parses text content until a tag is encountered
    /// 
    /// Text is everything between tags. This method:
    /// - Reads all characters until '&lt;' (start of next tag)
    /// - If NOT inside script/style tags, stops at '{' (code block)
    /// - If inside script/style tags, treats '{' as literal text
    /// - Handles multi-buffer reads if needed
    /// - Stops at '&lt;' or '{' (when not in script/style) without consuming it
    /// 
    /// The '&lt;' or '{' character is not consumed so ProcessTag/ProcessCodeBlock can handle it correctly.
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
                                    if (tagCh == ' ' || tagCh == '\t' || tagCh == '\r' || tagCh == '\n' || tagCh == '>')
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

            // Stop at '{' only if NOT inside script/style tags
            // EXCEPTION: If inside script/style and '{' is followed by '#' or '/', it's a directive - stop here
            // Regular JavaScript '{' characters should remain as literal text
            if (ch == '{')
            {
                if (!isInsideScriptOrStyle)
                {
                    // Not inside script/style - stop at '{' to process as code block
                    break;
                }
                else if (this.IsDirectiveAtPosition())
                {
                    // Inside script/style and it's a directive - stop here so it can be processed
                    break;
                }
                // Inside script/style and NOT a directive - treat '{' as literal text and continue
            }

            // Accumulate characters
            chars.Add(ch);
            this.Position++;
        }

        // Convert accumulated characters to string
        return new string(chars.ToArray());
    }

    /// <summary>
    /// Checks if the current script tag is at the bottom of the document (after all content).
    /// Bottom scripts are hoisted and rendered separately via RenderScripts().
    /// </summary>
    private bool IsScriptAtBottom()
    {
        return this.IsTagAtBottom();
    }

    /// <summary>
    /// Checks if the current style tag is at the bottom of the document (after all content).
    /// Bottom styles are hoisted and rendered separately via RenderStyles().
    /// </summary>
    private bool IsStyleAtBottom()
    {
        return this.IsTagAtBottom();
    }

    /// <summary>
    /// Checks if the current tag is at the bottom of the content.
    /// A tag is considered "at the bottom" if what follows is only:
    /// - Whitespace
    /// - Closing tags (&lt;/body&gt;, &lt;/html&gt;, etc.)
    /// - Other script/style tags that are also at the end (these are also hoisted)
    /// - End of stream
    /// 
    /// Note: This recursively checks if following script/style tags are also at the bottom,
    /// allowing multiple script/style tags at the end to all be hoisted.
    /// </summary>
    private bool IsTagAtBottom()
    {
        var savedPosition = this.Position;
        var savedTotalCharsRead = this.TotalCharsRead;

        try
        {
            // Skip whitespace
            while (this.Position < this.Length || this.ReadMore())
            {
                if (this.Position >= this.Length)
                {
                    break;
                }

                var ch = this.Buffer[this.Position];

                if (char.IsWhiteSpace(ch))
                {
                    this.Position++;
                    continue;
                }

                // Check for opening tags
                if (ch == '<')
                {
                    this.Position++;
                    if (this.Position >= this.Length && !this.ReadMore())
                    {
                        break;
                    }

                    var nextCh = this.Buffer[this.Position];

                    // Check for closing tags like </body>, </html>, etc.
                    if (nextCh == '/')
                    {
                        // Found closing tag - skip it
                        this.Position++;
                        // Skip until '>'
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

                        continue;
                    }

                    // Check if this is a script or style opening tag
                    // If we find another script/style tag, both are considered "at the bottom"
                    // (they're both end tags that should be hoisted together)
                    var tagName = this.ReadTagName();
                    if (tagName != null)
                    {
                        var normalizedTagName = tagName.ToLowerInvariant();
                        if (normalizedTagName == "script" || normalizedTagName == "style")
                        {
                            // Found another script/style tag - both are at the bottom
                            // Return true immediately (no need to check further)
                            return true;
                        }
                        else
                        {
                            // Not a script/style tag - restore position and return false
                            this.Position = savedPosition;
                            this.TotalCharsRead = savedTotalCharsRead;
                            return false;
                        }
                    }
                    else
                    {
                        // Couldn't read tag name - restore position and return false
                        this.Position = savedPosition;
                        this.TotalCharsRead = savedTotalCharsRead;
                        return false;
                    }
                }

                // Found non-whitespace, non-tag content - not at bottom
                return false;
            }

            // Reached end with only whitespace/closing tags - this is a bottom tag
            return true;
        }
        finally
        {
            // Restore position
            this.Position = savedPosition;
            this.TotalCharsRead = savedTotalCharsRead;
        }
    }

    /// <summary>
    /// Reads a tag name from the current position.
    /// Assumes we're positioned right after '&lt;'.
    /// </summary>
    private string? ReadTagName()
    {
        var tagNameBuilder = new StringBuilder();

        while (this.Position < this.Length || this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];

            // Stop at whitespace, '>', or '/'
            if (char.IsWhiteSpace(ch) || ch == '>' || ch == '/')
            {
                break;
            }

            tagNameBuilder.Append(ch);
            this.Position++;
        }

        var tagName = tagNameBuilder.ToString();
        return string.IsNullOrEmpty(tagName) ? null : tagName;
    }
}