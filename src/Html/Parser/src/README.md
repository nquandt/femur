# HtmlParser

A streaming HTML 2.0 parser that reads from a `Stream` and builds an Abstract Syntax Tree (AST) of nodes.

## Overview

`HtmlParser` extends `StreamParser<DocumentNode>` and implements a single-pass parser that processes HTML content character-by-character, building a tree structure of nodes as it reads.

## Parsing Strategy

The parser uses a **streaming approach** with the following characteristics:

- **Sliding Buffer**: Reads stream in chunks (default 4KB) to handle large files efficiently
- **Absolute Position Tracking**: Maintains position across buffer boundaries for accurate location tracking
- **Element Stack**: Uses a stack to match opening/closing tags and maintain parent-child relationships
- **Single Pass**: Processes tokens in one pass without a separate tokenization phase
- **Location Tracking**: Every node includes `SourceLocation` information (start position and length)

## Architecture

### Base Class: StreamParser

`HtmlParser` extends `StreamParser<DocumentNode>`, which provides:

1. **Buffer Management**: Handles reading chunks from the stream
2. **Position Tracking**: Manages buffer position and absolute byte position
3. **Template Method Pattern**: Defines the parsing algorithm:
   - `CreateDocument()` - Creates the root document node
   - `InitializeParsing()` - Sets up parsing state
   - `ProcessCharacter()` - Processes each character (main parsing logic)
   - `Cleanup()` - Cleans up resources

### Parsing Flow

```
1. CreateDocument() → Creates DocumentNode
2. InitializeParsing() → Sets up stacks and state
3. Main Loop (for each character):
   - ReadMore() → Ensures buffer has data
   - ProcessCharacter() → Routes to appropriate handler
4. Cleanup() → Returns buffer to pool
```

## Core Data Structures

### State Variables

- `_document`: Reference to the root document node
- `_currentParent`: Current container node where new children are added
- `_elementStack`: Stack of `ElementNode` objects for matching opening/closing tags
- `_isInsideScriptOrStyle`: Flag to handle script/style tags specially

### Void Elements

HTML void elements (like `<br>`, `<img>`, `<input>`) cannot have children and don't need closing tags. These are tracked in a static `HashSet` and are never pushed onto the element stack.

## Parsing Flow

### Character Processing

The main entry point is `ProcessCharacter()`, which routes characters to appropriate handlers:

```csharp
if (ch == '<')
    ProcessTag()      // Tag processing
else
    ProcessTextContent()  // Text content
```

### Tag Processing

When encountering `<`, the parser examines the next character to determine tag type:

1. **`<!`** → Special tag (comment, CDATA, DOCTYPE)
2. **`</`** → Closing tag
3. **Otherwise** → Opening tag

#### Opening Tags (`ProcessOpeningTag`)

1. Peek ahead to check for SVG tags (special handling)
2. Parse tag name and attributes (`ParseOpeningTag`)
3. Create `ElementNode` and add to `_currentParent.Children`
4. If not void and not self-closing, push onto `_elementStack`
5. Update `_currentParent` to the new element
6. Track script/style tags to set `_isInsideScriptOrStyle` flag

#### Closing Tags (`ProcessClosingTag`)

1. Parse closing tag name (`ParseClosingTag`)
2. Pop elements from `_elementStack` until finding a matching tag name
3. Handle mismatched tags gracefully (similar to browser behavior)
4. Update `_currentParent` to the matched element's parent
5. Clear `_isInsideScriptOrStyle` flag if exiting script/style tag

#### Special Tags (`ProcessSpecialTag`)

Handles three types of special tags:

- **Comments**: `<!-- comment -->`
- **CDATA**: `<![CDATA[...]]>`
- **DOCTYPE**: `<!DOCTYPE ...>`

These don't affect the element hierarchy, so `_currentParent` remains unchanged.

### Text Content Processing

`ProcessTextContent()` handles everything between tags:

1. Reads all characters until encountering `<` (start of next tag)
2. Inside script/style tags: Preserves ALL content including whitespace
3. Outside script/style tags: Filters out pure whitespace text nodes
4. Creates `TextNode` with location tracking

**Script/Style Handling**: Inside `<script>` and `<style>` tags, the parser:
- Preserves all whitespace and content exactly as written
- Only stops at `</script>` or `</style>` closing tags
- Treats other `<` characters as literal text

### Attribute Parsing

Attributes are parsed with support for:

- **Quoted values**: `attr="value"` or `attr='value'`
- **Unquoted values**: `attr=value` (until whitespace or `>`)
- **Boolean attributes**: `attr` (no value, stored as empty string)
- **Self-closing tags**: `<tag />` or `<tag/>`

## Special Features

### SVG Handling

When encountering an `<svg>` tag, the parser:

1. Rewinds to the opening `<svg>` tag position
2. Creates a `SvgSubStream` wrapper that reads until `</svg>`
3. Delegates parsing to `XmlParser` for the SVG block
4. Adds the resulting `XmlElementNode` to the HTML AST
5. SVG elements don't go on the element stack (foreign elements)

**Limitation**: SVG blocks must fit within a single buffer (typically 4KB). Blocks spanning multiple buffers will throw an exception.

### Location Tracking

Every node includes a `Location` property (`SourceLocation`) that tracks:
- **Start Position**: Absolute byte position in the stream
- **Length**: Number of bytes the node spans

This enables:
- Error reporting with exact positions
- Source mapping
- Round-trip editing

## Example Flow

For HTML like:
```html
<div>
  <p>Hello</p>
  <img src="test.jpg" />
</div>
```

The parsing flow:

1. **`<div>`**: Create `ElementNode`, push onto stack, set as `_currentParent`
2. **Text "  "**: Filtered out (whitespace-only)
3. **`<p>`**: Create `ElementNode`, push onto stack, set as `_currentParent`
4. **Text "Hello"**: Create `TextNode`, add to `<p>` children
5. **`</p>`**: Pop `<p>` from stack, restore `_currentParent` to `<div>`
6. **Text "  "**: Filtered out
7. **`<img ... />`**: Create `ElementNode` (void element), add to `<div>`, don't push stack
8. **`</div>`**: Pop `<div>` from stack, restore `_currentParent` to document

## Error Handling

The parser handles malformed HTML gracefully:

- **Mismatched closing tags**: Pops up the stack until finding a match (browser-like behavior)
- **Unclosed tags**: Elements remain on stack (can be detected after parsing)
- **Invalid characters**: Handled according to HTML spec (generally ignored or treated as text)

## Performance Considerations

- **Streaming**: Processes large files without loading entire content into memory
- **Buffer Pooling**: Uses `ArrayPool<byte>` for efficient buffer management
- **Single Pass**: No backtracking or multiple passes required
- **Minimal Allocations**: Reuses `StringBuilder` and buffers where possible

## Usage

```csharp
// From stream
using var stream = new FileStream("page.html", FileMode.Open);
var parser = new HtmlParser(stream);
var document = parser.Parse();

// From string
var document = HtmlParser.Parse("<html>...</html>");

// From bytes
var bytes = Encoding.UTF8.GetBytes("<html>...</html>");
var document = HtmlParser.Parse(bytes);
```

## Key Methods

- `ProcessCharacter()`: Main character routing logic
- `ProcessTag()`: Routes to opening/closing/special tag handlers
- `ProcessOpeningTag()`: Handles opening tags and updates stack
- `ProcessClosingTag()`: Handles closing tags and updates stack
- `ProcessTextContent()`: Handles text between tags
- `ParseOpeningTag()`: Parses tag name, attributes, self-closing indicator
- `ParseSpecialTag()`: Parses comments, CDATA, DOCTYPE
- `ParseSvgAsXml()`: Special handling for SVG blocks

