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

### Phase 1: Block Structure (Partially Implemented)

- ✅ ATX headings (`# Heading`)
- ✅ Thematic breaks (`---`, `***`, `___`)
- ✅ Block quotes (`> text`)
- ✅ Fenced code blocks (`` ```code``` ``)
- ✅ Indented code blocks (4+ spaces)
- ✅ Lists (ordered and unordered)
- ⚠️ Setext headings (underlined with `===` or `---`) - Not yet implemented
- ⚠️ HTML blocks - Basic structure exists, needs full CommonMark HTML block rules
- ⚠️ Link reference definitions - Not yet implemented
- ⚠️ Blank line handling - Needs refinement

### Phase 2: Inline Structure (Placeholder)

The inline parsing is currently a placeholder. Full CommonMark inline parsing requires:

- ⚠️ Emphasis (`*text*`, `_text_`)
- ⚠️ Strong emphasis (`**text**`, `__text__`)
- ⚠️ Links (`[text](url)`, `[text][ref]`)
- ⚠️ Images (`![alt](url)`, `![alt][ref]`)
- ⚠️ Code spans (`` `code` ``)
- ⚠️ Autolinks (`<url>`)
- ⚠️ Raw HTML (`<tag>`)
- ⚠️ Hard line breaks (two spaces + newline, or backslash + newline)
- ⚠️ Soft line breaks (single newline)

The CommonMark spec describes a sophisticated delimiter stack algorithm for parsing nested emphasis and links, which needs to be implemented.

## AST Node Types

### Block-Level Nodes

- `MarkdownDocumentNode` - Root document node
- `HeadingNode` - ATX or Setext heading (level 1-6)
- `ParagraphNode` - Paragraph containing inline content
- `BlockQuoteNode` - Block quote containing other blocks
- `CodeBlockNode` - Code block (fenced or indented)
- `ListNode` - Ordered or unordered list
- `ListItemNode` - Individual list item
- `ThematicBreakNode` - Horizontal rule
- `HtmlBlockNode` - Raw HTML block

### Inline-Level Nodes

- `EmphasisNode` - Emphasis (`*text*`, `_text_`)
- `StrongEmphasisNode` - Strong emphasis (`**text**`, `__text__`)
- `LinkNode` - Link with URL and optional title
- `ImageNode` - Image with URL and optional title
- `CodeSpanNode` - Inline code span
- `HardLineBreakNode` - Hard line break
- `SoftLineBreakNode` - Soft line break
- `TextNode` - Plain text content (from Abstractions)

## Usage

```csharp
using Femur.Parsers.Markdown;

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

