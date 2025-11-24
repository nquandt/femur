# XmlParser

A streaming XML parser that reads from a `Stream` and builds an Abstract Syntax Tree (AST) of nodes.

## Overview

`XmlParser` extends `StreamParser<XmlDocumentNode>` and implements a single-pass parser that processes XML content character-by-character, building a tree structure of nodes. XML parsing is stricter than HTML - all tags must be closed, attributes must be quoted, and tag names are case-sensitive.

## Parsing Strategy

The parser uses a **streaming approach** with the following characteristics:

- **Sliding Buffer**: Reads stream in chunks (default 4KB) to handle large files efficiently
- **Absolute Position Tracking**: Maintains position across buffer boundaries for accurate location tracking
- **Element Stack**: Uses a stack to match opening/closing tags with case-sensitive matching
- **Single Pass**: Processes tokens in one pass without a separate tokenization phase
- **XML-Specific Features**: Handles processing instructions, namespaces, and strict tag matching

## Architecture

### Base Class: StreamParser

`XmlParser` extends `StreamParser<XmlDocumentNode>`, which provides:

1. **Buffer Management**: Handles reading chunks from the stream
2. **Position Tracking**: Manages buffer position and absolute byte position
3. **Template Method Pattern**: Defines the parsing algorithm:
   - `CreateDocument()` - Creates the root XML document node
   - `InitializeParsing()` - Sets up parsing state
   - `ProcessCharacter()` - Processes each character (main parsing logic)
   - `Cleanup()` - Cleans up resources

### Parsing Flow

```
1. CreateDocument() → Creates XmlDocumentNode
2. InitializeParsing() → Sets up stacks and state
3. Main Loop (for each character):
   - ReadMore() → Ensures buffer has data
   - ProcessCharacter() → Routes to appropriate handler
4. Cleanup() → Returns buffer to pool
```

## Core Data Structures

### State Variables

- `_document`: Reference to the root XML document node
- `_currentParent`: Current container node where new children are added
- `_elementStack`: Stack of `XmlElementNode` objects for matching opening/closing tags

### XML-Specific Features

Unlike HTML, XML has:
- **No void elements**: All tags must be closed (either `<tag></tag>` or `<tag />`)
- **Case-sensitive**: Tag names must match exactly (case-sensitive)
- **Quoted attributes**: All attribute values must be quoted (single or double quotes)
- **Namespaces**: Supports namespace prefixes (`prefix:localname`)
- **Processing Instructions**: Supports `<?target data?>` syntax

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

1. **`<!`** → Special tag (comment, CDATA)
2. **`<?`** → Processing instruction (`<?xml version="1.0"?>`)
3. **`</`** → Closing tag
4. **Otherwise** → Opening tag

#### Opening Tags (`ProcessOpeningTag`)

1. Parse tag name and attributes (`ParseOpeningTag`)
2. Extract namespace prefix if present (`prefix:localname`)
3. Create `XmlElementNode` and add to `_currentParent.Children`
4. If not self-closing, push onto `_elementStack`
5. Update `_currentParent` to the new element
6. Handle namespace declarations (`xmlns` and `xmlns:prefix`)

#### Closing Tags (`ProcessClosingTag`)

1. Parse closing tag name (case-sensitive)
2. Pop elements from `_elementStack` until finding an **exact match** (case-sensitive)
3. Update `_currentParent` to the matched element's parent
4. If no match found and stack is empty, restore to document root

**Key Difference from HTML**: XML requires exact case-sensitive matching. `<Tag>` and `</tag>` would be considered mismatched.

#### Processing Instructions (`ProcessProcessingInstruction`)

Handles XML processing instructions like `<?xml version="1.0"?>`:

1. Parse target name (e.g., "xml")
2. Read content until `?>`
3. Create `ProcessingInstructionNode`
4. If target is "xml", store in `_document.XmlDeclaration`

#### Special Tags (`ProcessSpecialTag`)

Handles two types of special tags:

- **Comments**: `<!-- comment -->`
- **CDATA**: `<![CDATA[...]]>`

These don't affect the element hierarchy, so `_currentParent` remains unchanged.

### Text Content Processing

`ProcessTextContent()` handles everything between tags:

1. Reads all characters until encountering `<` (start of next tag)
2. Filters out pure whitespace text nodes (XML preserves whitespace, but parser filters for efficiency)
3. Creates `TextNode` with location tracking

**Note**: XML preserves whitespace by default, but this parser filters whitespace-only text nodes for efficiency. Full whitespace preservation can be enabled if needed.

### Attribute Parsing

Attributes are parsed with strict XML rules:

- **Must be quoted**: `attr="value"` or `attr='value'` (required)
- **Escaped quotes**: Supports `\"` and `\'` escaping within attribute values
- **Namespace declarations**: 
  - `xmlns="uri"` → Sets default namespace URI
  - `xmlns:prefix="uri"` → Sets namespace URI for prefix

**Key Difference from HTML**: XML requires all attribute values to be quoted. Unquoted attributes are not valid XML.

### Namespace Handling

XML supports namespaces via prefixes:

- **Tag names**: `prefix:localname` (e.g., `svg:circle`)
- **Namespace prefix**: Extracted and stored in `XmlElementNode.NamespacePrefix`
- **Namespace URI**: Extracted from `xmlns` or `xmlns:prefix` attributes

The parser extracts namespace information but doesn't fully resolve namespace URIs (that would require maintaining a namespace context stack).

## Special Features

### XML Declaration

The XML declaration (`<?xml version="1.0"?>`) is:
- Parsed as a `ProcessingInstructionNode`
- Stored in `XmlDocumentNode.XmlDeclaration` for easy access
- Preserved in the document structure

### Location Tracking

Every node includes a `Location` property (`SourceLocation`) that tracks:
- **Start Position**: Absolute byte position in the stream
- **Length**: Number of bytes the node spans

This enables:
- Error reporting with exact positions
- Source mapping
- Round-trip editing

## Example Flow

For XML like:
```xml
<?xml version="1.0"?>
<root>
  <child attr="value">Text</child>
  <self-closing />
</root>
```

The parsing flow:

1. **`<?xml version="1.0"?>`**: Create `ProcessingInstructionNode`, store in `_document.XmlDeclaration`
2. **`<root>`**: Create `XmlElementNode`, push onto stack, set as `_currentParent`
3. **Text "  "**: Filtered out (whitespace-only)
4. **`<child attr="value">`**: Create `XmlElementNode` with attribute, push onto stack, set as `_currentParent`
5. **Text "Text"**: Create `TextNode`, add to `<child>` children
6. **`</child>`**: Pop `<child>` from stack (case-sensitive match), restore `_currentParent` to `<root>`
7. **Text "  "**: Filtered out
8. **`<self-closing />`**: Create `XmlElementNode` (self-closing), add to `<root>`, don't push stack
9. **`</root>`**: Pop `<root>` from stack, restore `_currentParent` to document

## Error Handling

The parser handles XML with strict rules:

- **Case-sensitive matching**: `<Tag>` and `</tag>` are considered mismatched
- **Unclosed tags**: Elements remain on stack (can be detected after parsing)
- **Unquoted attributes**: Parsed but may not be valid XML
- **Invalid characters**: Generally treated as text content

## Performance Considerations

- **Streaming**: Processes large files without loading entire content into memory
- **Buffer Pooling**: Uses `ArrayPool<byte>` for efficient buffer management
- **Single Pass**: No backtracking or multiple passes required
- **Minimal Allocations**: Reuses `StringBuilder` and buffers where possible

## Usage

```csharp
// From stream
using var stream = new FileStream("data.xml", FileMode.Open);
var parser = new XmlParser(stream);
var document = parser.Parse();

// From string
var document = XmlParser.Parse("<root>...</root>");

// From bytes
var bytes = Encoding.UTF8.GetBytes("<root>...</root>");
var document = XmlParser.Parse(bytes);
```

## Key Methods

- `ProcessCharacter()`: Main character routing logic
- `ProcessTag()`: Routes to opening/closing/special/processing instruction handlers
- `ProcessOpeningTag()`: Handles opening tags and updates stack
- `ProcessClosingTag()`: Handles closing tags with case-sensitive matching
- `ProcessProcessingInstruction()`: Handles `<?target data?>` syntax
- `ProcessSpecialTag()`: Parses comments and CDATA
- `ProcessTextContent()`: Handles text between tags
- `ParseOpeningTag()`: Parses tag name, attributes, namespaces, self-closing indicator

## Differences from HTML Parser

| Feature | HTML | XML |
|---------|------|-----|
| Case sensitivity | Case-insensitive | Case-sensitive |
| Void elements | Yes (`<br>`, `<img>`) | No (all must be closed) |
| Attribute quotes | Optional | Required |
| Unquoted attributes | Allowed | Not valid |
| Self-closing | `<tag />` or `<tag/>` | `<tag />` only |
| Processing instructions | No | Yes (`<?target?>` |
| Namespaces | No | Yes (`prefix:name`) |

