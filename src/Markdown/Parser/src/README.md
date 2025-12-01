# MarkdownParser

A streaming Markdown parser that reads from a `Stream` and builds an Abstract Syntax Tree (AST) of nodes, implementing the CommonMark 0.31.2 specification.

## Overview

`MarkdownParser` extends `StreamParser<MarkdownDocumentNode>` and implements a parser that processes Markdown content character-by-character, building a tree structure of nodes as it reads.

## Parsing Strategy

The parser uses a **streaming approach** with the following characteristics:

- **Sliding Buffer**: Reads stream in chunks (default 4KB) to handle large files efficiently
- **Absolute Position Tracking**: Maintains position across buffer boundaries for accurate location tracking
- **Two-Phase Parsing**: Implements CommonMark's two-phase approach:
  1. **Phase 1: Block Structure** - Identifies block-level elements (headings, paragraphs, lists, blockquotes, code blocks, etc.)
  2. **Phase 2: Inline Structure** - Parses inline content within blocks (emphasis, links, code spans, etc.)
- **Single Pass**: Processes tokens in one pass without a separate tokenization phase
- **Location Tracking**: Every node includes `SourceLocation` information (start position and length)

### Phase 1: Block Structure Parsing

In Phase 1, the parser reads the input line-by-line and identifies block-level constructs:

1. **Line-by-line Scanning**: Reads the entire stream character-by-character and accumulates complete lines
2. **Block Marker Detection**: At the start of each line, checks for:
   - ATX headings (`# Heading`)
   - Thematic breaks (`---`, `***`, `___`)
   - Fenced code blocks (`` ``` `` or `~~~`)
   - Indented code blocks (4+ spaces)
   - Block quotes (`>`)
   - Lists (ordered or unordered)
   - HTML blocks (raw HTML content)
   - Setext headings (underlined with `===` or `---`)
   - Link reference definitions
3. **Block Nesting**: Some blocks can contain other blocks:
   - Block quotes can contain paragraphs, lists, headings, code blocks, etc.
   - List items can contain paragraphs, nested lists, code blocks, block quotes, etc.
   - List items determine "loose" vs "tight" formatting
4. **Paragraph Accumulation**: When no block marker is found, text is accumulated as a paragraph until a blank line or block construct is encountered

### Phase 2: Inline Structure Parsing

In Phase 2, the parser traverses the AST and processes inline content within:
- Paragraphs
- Headings  
- Emphasis and strong emphasis nodes
- Link and image text

The inline parser recognizes:
- Code spans (backticks: `` `code` ``)
- Emphasis (`*text*` or `_text_`)
- Strong emphasis (`**text**` or `__text__`)
- Links (`[text](url)` or `[ref]`)
- Images (`![alt](url)` or `![ref]`)
- Line breaks (hard: two spaces + newline, or backslash + newline; soft: single newline)

### Phase 3: Smart Punctuation (Optional)

After creating the AST, the parser can optionally apply smart punctuation transformations:
- Straight quotes (`"`, `'`) → curly quotes (`"`, `"`, `'`, `'`)
- Hyphens (`-`, `--`, `---`) → appropriate dashes (hyphen, en-dash, em-dash)
- Ellipsis (`...`) → proper ellipsis character (…)

These transformations are skipped in code blocks and code spans, and respect escaped sequences.

## Architecture

### Base Class: StreamParser

`MarkdownParser` extends `StreamParser<MarkdownDocumentNode>`, which provides:

1. **Buffer Management**: Handles reading chunks from the stream
2. **Position Tracking**: Manages buffer position and absolute byte position
3. **Template Method Pattern**: Defines the parsing algorithm:
   - `CreateDocument()` - Creates the root Markdown document node
   - `InitializeParsing()` - Sets up parsing state
   - `ProcessCharacter()` - Processes each character (main parsing logic)
   - `Cleanup()` - Cleans up resources and processes remaining content

### Parsing Flow

```
1. CreateDocument() → Creates MarkdownDocumentNode
2. InitializeParsing() → Sets up stacks and state
3. Main Loop (for each character):
   - ReadMore() → Ensures buffer has data
   - ProcessCharacter() → Routes to appropriate block handler
4. Cleanup() → Processes remaining text as paragraph
```

## CommonMark Implementation Status

**Overall Status**: ~70% implemented - Most core features complete, working on complex inline parsing

See [COMMONMARK_IMPLEMENTATION_STATUS.md](../../COMMONMARK_IMPLEMENTATION_STATUS.md) for comprehensive status breakdown.

### Phase 1: Block Structure Parsing

**✅ Fully Implemented:**
- ATX headings (`# Heading`) - All levels 1-6
- Setext headings (underlined with `===` or `---`) - Full CommonMark compliance
- Thematic breaks (`---`, `***`, `___`) with proper precedence over Setext underlines
- Block quotes (`> text`) with nested content and lazy continuation
- Fenced code blocks (`` ```code``` `` and `~~~code~~~`) with info strings
- Indented code blocks (4+ spaces) with proper handling
- Unordered lists (`-`, `*`, `+`) with nested items
- Ordered lists (`1.`, `2.`, etc.) with custom start numbers
- List tightness detection - `IsLoose` set based on blank lines between items
- Paragraphs with proper blank line termination
- Link reference definitions with multi-line support and URL/title parsing
- HTML blocks - Full CommonMark 7-type system:
  - Type 1: `<script>`, `<style>`, `<pre>`, `<iframe>` tags (line-based)
  - Type 2: HTML comments `<!-- ... -->`
  - Type 3: Processing instructions `<? ... ?>`
  - Type 4: Declarations `<! ... >`
  - Type 5: CDATA sections `<![CDATA[ ... ]]>`
  - Type 6: Known block tags (address, article, aside, blockquote, etc.)
  - Type 7: Complete open/closing tags with blank line termination

### Phase 2: Inline Structure Parsing

**✅ Fully Implemented (Simple Cases):**
- Code spans (backticks with matching count)
- Basic emphasis (`*text*`, `_text_`)
- Basic strong emphasis (`**text**`, `__text__`)
- Inline links (`[text](url)` with optional title)
- Reference links (`[text][ref]`, `[text]`) with ID normalization
- Images (`![alt](url)` with optional title) and reference images
- Hard line breaks (two spaces + newline or backslash + newline)
- Soft line breaks (single newline)

**❌ Complex Cases - Requires Delimiter Stack Algorithm (Not Implemented):**
- Complex emphasis nesting (e.g., `***foo**bar*`)
- Emphasis precedence and spacing rules per CommonMark section 6.3
- Autolinks (`<http://example.com>`)
- Raw HTML inline (`<span>text</span>`)
- HTML entities (`&amp;`, `&#123;`, etc.)

**Current Limitation**: Uses simplified pattern matching instead of CommonMark's delimiter stack algorithm, preventing correct parsing of complex emphasis and link scenarios. This is the **primary remaining work item** for full CommonMark compliance.

### Phase 3: Smart Punctuation (Optional Feature)

**✅ Fully Implemented:**
- Smart quotes ("curly" quotes with delimiter stack)
- Smart dashes (`--` → en-dash, `---` → em-dash)
- Ellipsis (`...` → proper character)
- Apostrophe detection
- Escaping support for smart punctuation

## AST Node Types

### Block-Level Nodes

Block-level nodes represent the structural elements that make up the main sections of a Markdown document. These nodes can typically contain other nodes (usually inline content).

- **`MarkdownDocumentNode`** - The root node representing the entire Markdown document. Contains all top-level block elements.
  - Children: Block-level nodes

- **`HeadingNode`** - Represents a heading created with ATX (`#`) or Setext (`===`/`---`) syntax.
  - Properties: `Level` (int 1-6), location information
  - Children: Inline content (text, emphasis, links, code spans, etc.)
  - Example: `# Main Heading`, `## Sub-heading`

- **`ParagraphNode`** - Represents a paragraph containing text and inline formatting.
  - Children: Inline content (text, emphasis, strong emphasis, links, images, code spans, line breaks)
  - Example: `This is a paragraph with **bold** text`

- **`BlockQuoteNode`** - Represents a quoted block with `>` marker.
  - Children: Block-level nodes (paragraphs, lists, nested blockquotes, etc.)
  - Example: `> This is quoted text`

- **`CodeBlockNode`** - Represents a code block, either fenced (```code```) or indented.
  - Properties:
    - `Content` (string): The code content
    - `Info` (string?): Language identifier for fenced code blocks (used for syntax highlighting)
    - `IsFenced` (bool): Distinguishes between fenced and indented code blocks
  - Leaf Node: Contains no children
  - Example: ` ```csharp\nvar x = 42;\n``` ` or 4-space indented code

- **`ListNode`** - Represents an ordered or unordered list.
  - Properties:
    - `IsOrdered` (bool): True for numbered lists (`1.`, `2.`), false for bullet lists (`-`, `*`, `+`)
    - `StartNumber` (int): Starting number for ordered lists (default 1)
    - `BulletChar` (char): Bullet character for unordered lists (`-`, `*`, or `+`)
    - `IsLoose` (bool): True if list items are separated by blank lines (affects rendering)
  - Children: `ListItemNode` instances

- **`ListItemNode`** - Represents a single item within a list.
  - Children: Block-level nodes (paragraphs, nested lists, code blocks, etc.)
  - Can contain multiple block-level elements when the list is "loose"

- **`ThematicBreakNode`** - Represents a horizontal rule or thematic break.
  - Properties: `---`, `***`, or `___` with 3+ characters
  - Leaf Node: Contains no children

- **`HtmlBlockNode`** - Represents raw HTML content that should be rendered as-is.
  - Properties:
    - `Content` (string): The raw HTML content
  - Leaf Node: Contains no children
  - Example: `<div>\n<script>\nalert('test');\n</script>\n</div>`

### Inline Nodes

Inline nodes represent formatting and content that appears within block-level elements. These nodes are children of paragraphs, headings, list items, blockquotes, etc.

- **`MarkdownTextNode`** - Plain text content.
  - Properties:
    - `Content` (string): The text content
  - Leaf Node: Contains no children
  - Note: Typically the most common node type in the AST

- **`EmphasisNode`** - Represents emphasis (italic) formatting using `*text*` or `_text_`.
  - Children: Inline content (typically text, but can contain other inline elements)
  - Rendering: Usually rendered as `<em>` or italic text

- **`StrongEmphasisNode`** - Represents strong emphasis (bold) formatting using `**text**` or `__text__`.
  - Children: Inline content
  - Rendering: Usually rendered as `<strong>` or bold text

- **`LinkNode`** - Represents a hyperlink with `[text](url)`, `[text][ref]`, or shortcut reference syntax.
  - Properties:
    - `Url` (string): The target URL
    - `Title` (string?): Optional link title (displayed on hover in HTML)
  - Children: Inline content for the link text (can include images, emphasis, text, etc.)
  - Example: `[Click here](https://example.com "Title")` or `[reference link][ref-id]`

- **`ImageNode`** - Represents an embedded image with `![alt](url)`, `![alt][ref]`, or shortcut reference syntax.
  - Properties:
    - `Url` (string): The image URL
    - `Title` (string?): Optional image title
  - Children: Inline content for the alt text (typically text or formatting)
  - Example: `![alt text](image.png "Image Title")` or `![referenced image][img-ref]`

- **`CodeSpanNode`** - Represents inline code using backticks `` `code` `` or multiple backticks.
  - Properties:
    - `Content` (string): The code content (literal, no inline parsing)
  - Leaf Node: Contains no children
  - Example: `` `const x = 42;` `` or ``` `` `backtick` `` ```

- **`HardLineBreakNode`** - Represents a forced line break created by:
  - Two or more spaces at the end of a line followed by newline, OR
  - Backslash followed by newline (`\` + newline)
  - Rendering: Usually rendered as `<br>` in HTML
  - Leaf Node: Contains no children

- **`SoftLineBreakNode`** - Represents a line break within a paragraph that isn't forced.
  - Created by a single newline within a paragraph
  - Rendering: Usually rendered as a space in HTML
  - Leaf Node: Contains no children

### Node Hierarchy

```
MarkdownContainerNode (base class for nodes that can have children)
├── MarkdownDocumentNode
├── HeadingNode
├── ParagraphNode
├── BlockQuoteNode
├── ListNode
├── ListItemNode
├── EmphasisNode
├── StrongEmphasisNode
├── LinkNode
└── ImageNode

MarkdownLeafNode (base class for nodes without children)
├── CodeBlockNode
├── ThematicBreakNode
├── HtmlBlockNode
├── CodeSpanNode
├── HardLineBreakNode
├── SoftLineBreakNode
└── MarkdownTextNode
```

## Usage

### Basic Parsing

```csharp
using Femur.Markdown.Parser;

// Parse from stream
using var stream = File.OpenRead("document.md");
var parser = new MarkdownParser(stream);
var document = parser.Parse();

// Parse from string
var document = MarkdownParser.Parse("# Hello World\n\nThis is a paragraph.");

// Parse from byte array
var bytes = Encoding.UTF8.GetBytes("# Hello World");
var document = MarkdownParser.Parse(bytes);
```

### Processing the AST

After parsing, you can traverse the resulting AST using the `MarkdownAstWalker` from `Femur.Markdown.Abstractions`:

```csharp
using Femur.Markdown.Abstractions;

// Create a custom walker to process the document
public class HeadingCollector : MarkdownAstWalker
{
    public List<string> Headings { get; } = new();

    protected override void VisitHeading(HeadingNode node)
    {
        // Extract heading text from children
        var textCollector = new TextCollector();
        foreach (var child in node.Children)
        {
            textCollector.VisitNode(child);
        }

        Headings.Add($"Level {node.Level}: {textCollector.Text}");
        base.VisitHeading(node);
    }
}

// Use the walker
var document = MarkdownParser.Parse("# Main Title\n\n## Subtitle");
var collector = new HeadingCollector();
collector.Walk(document);

foreach (var heading in collector.Headings)
{
    Console.WriteLine(heading);
}
```

### Working with Specific Node Types

```csharp
// Extract all links
public class LinkExtractor : MarkdownAstWalker
{
    public List<(string Text, string Url)> Links { get; } = new();

    protected override void VisitLink(LinkNode node)
    {
        var text = string.Join("", node.Children.OfType<MarkdownTextNode>().Select(n => n.Content));
        Links.Add((text, node.Url));
        base.VisitLink(node);
    }
}

// Extract all code blocks
public class CodeBlockExtractor : MarkdownAstWalker
{
    public List<(string? Language, string Code)> CodeBlocks { get; } = new();

    protected override void VisitCodeBlock(CodeBlockNode node)
    {
        CodeBlocks.Add((node.Info, node.Content));
        // No need to call base - code blocks have no children
    }
}
```

## Implementation Notes

### Character-by-Character vs Block-by-Block

Markdown parsing is naturally block-oriented, while `StreamParser` processes character-by-character. The current implementation handles this by:

1. Detecting block markers at the start of lines
2. Processing entire blocks when markers are detected
3. Accumulating text for paragraphs when no block markers are present

### CommonMark Compliance

This parser aims to implement the CommonMark 0.31.2 specification. However, full compliance requires:

1. Complete implementation of all block types
2. Full inline parsing with delimiter stack algorithm
3. Proper handling of edge cases (nested structures, precedence rules, etc.)
4. Comprehensive test coverage against CommonMark test suite

The current implementation provides a foundation that can be extended to full CommonMark compliance.

## Future Enhancements

- Complete inline parsing implementation with delimiter stack
- Setext heading support
- Full HTML block parsing per CommonMark rules
- Link reference definition parsing
- Better handling of tight vs loose lists
- Support for CommonMark extensions (tables, footnotes, etc.)

