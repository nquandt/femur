# ChtmlParser

A streaming CHTML (Component HTML) parser that reads from a `Stream` and builds an Abstract Syntax Tree (AST) of nodes. CHTML extends HTML with component syntax, code blocks, directives, and optional YAML front matter.

## Overview

`ChtmlParser` extends `StreamParser<ChtmlDocumentNode>` and implements a single-pass parser that processes CHTML content character-by-character, building a tree structure of nodes. CHTML adds several features on top of HTML:

- **Components**: `<:ComponentName />` syntax for component references
- **Code Blocks**: `{expression}` for embedded expressions
- **Directives**: `{#if condition}...{/if}` and `{#for item in collection}...{/for}` for conditional/iterative rendering
- **Front Matter**: Optional YAML front matter delimited by `---`

## Parsing Strategy

The parser uses a **streaming approach** with the following characteristics:

- **Sliding Buffer**: Reads stream in chunks (default 4KB) to handle large files efficiently
- **Absolute Position Tracking**: Maintains position across buffer boundaries for accurate location tracking
- **Element Stack**: Uses a stack to match opening/closing tags and maintain parent-child relationships
- **Directive Stack**: Separate stack for matching opening/closing directives (`{#if}`/`{/if}`, `{#for}`/`{/for}`)
- **Single Pass**: Processes tokens in one pass without a separate tokenization phase
- **Front Matter Parsing**: Parses YAML front matter at document start if present

## Architecture

### Base Class: StreamParser

`ChtmlParser` extends `StreamParser<ChtmlDocumentNode>`, which provides:

1. **Buffer Management**: Handles reading chunks from the stream
2. **Position Tracking**: Manages buffer position and absolute byte position
3. **Template Method Pattern**: Defines the parsing algorithm:
   - `CreateDocument()` - Creates the root CHTML document node
   - `InitializeParsing()` - Sets up parsing state and parses front matter
   - `ProcessCharacter()` - Processes each character (main parsing logic)
   - `Cleanup()` - Cleans up resources

### Parsing Flow

```
1. CreateDocument() → Creates ChtmlDocumentNode
2. InitializeParsing() → Parses front matter (if present), sets up stacks and state
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
- `_directiveStack`: Stack of directive nodes (`IfNode`, `ForNode`) for matching closing directives
- `_isInsideScriptOrStyle`: Flag to handle script/style tags specially

### Void Elements

HTML void elements (like `<br>`, `<img>`, `<input>`) cannot have children and don't need closing tags. These are tracked in a static `HashSet` and are never pushed onto the element stack.

## Parsing Flow

### Front Matter Parsing

At initialization, the parser checks for YAML front matter:

1. Check if first line is exactly `---`
2. If yes, read lines until closing `---`
3. Parse YAML content using `YamlDotNet`
4. Store parsed data in `ChtmlDocumentNode.FrontMatter` (dictionary)
5. Store raw text in `ChtmlDocumentNode.FrontMatterRaw` (even if parsing fails)

If front matter is not found or invalid, parsing continues normally with HTML content.

### Character Processing

The main entry point is `ProcessCharacter()`, which routes characters to appropriate handlers:

```csharp
if (ch == '{')
    ProcessCodeBlock()  // Code block or directive
else if (ch == '<')
    ProcessTag()        // Tag processing
else
    ProcessTextContent()  // Text content
```

**Special Handling**: Inside `<script>` and `<style>` tags:
- Regular code blocks `{expression}` are treated as literal text (preserves JavaScript/CSS syntax)
- Directives `{#if}`, `{#for}`, `{/if}`, `{/for}` are still processed (allows conditional script/style)

### Tag Processing

When encountering `<`, the parser examines the next character to determine tag type:

1. **`<!`** → Special tag (comment, CDATA, DOCTYPE)
2. **`</`** → Closing tag
3. **`<:` or `<C:`** → Component tag (`<:ComponentName />` or `<C:ComponentName />`)
4. **Otherwise** → Opening tag

#### Component Tags (`ProcessComponentTag`)

Components use the `:` prefix to indicate component references:

- **Syntax**: `<:ComponentName />` or `<C:ComponentName />`
- **Self-closing**: `<:Header />`
- **With children**: `<:Layout>...</:Layout>`
- **With attributes**: `<:Layout title="Home" />`
- **Component names**: Support dots and relative paths (e.g., `Namespace.Component`, `./Component`)

Components are parsed similarly to regular elements but create `ComponentNode` objects instead of `ElementNode`.

#### Opening Tags (`ProcessOpeningTag`)

Similar to HTML parser:

1. Parse tag name and attributes (`ParseOpeningTag`)
2. Create `ElementNode` and add to `_currentParent.Children`
3. If not void and not self-closing, push onto `_elementStack`
4. Update `_currentParent` to the new element
5. Track script/style tags to set `_isInsideScriptOrStyle` flag

**Attribute Parsing**: Supports code blocks in attributes:
- `attr="{expression}"` - Code block in attribute value
- Handles nested braces and quoted strings within code blocks

#### Closing Tags (`ProcessClosingTag`)

1. Parse closing tag name (handles both `</tag>` and `</:ComponentName>`)
2. Pop elements from `_elementStack` until finding a matching tag name
3. Handle mismatched tags gracefully
4. Update `_currentParent` to the matched element's parent
5. **Script/Style Hoisting**: If closing a script/style tag at the bottom of the document, convert to `ScriptNode` or `StyleNode`

**Bottom Script/Style Detection**: Script and style tags at the end of the document (after all content) are "hoisted" and converted to special node types for separate rendering.

### Code Block Processing

Code blocks start with `{` and can be:

1. **Regular Expression**: `{props.Title}` → Creates `CodeNode`
2. **Opening Directive**: `{#if condition}` → Creates `IfNode` or `ForNode`
3. **Closing Directive**: `{/if}` or `{/for}` → Matches with opening directive

#### Regular Code Blocks (`ParseCodeBlock`)

- Reads content until closing `}`
- Nested braces are **not** supported (first `}` closes the block)
- Creates `CodeNode` with the content

#### Directives (`ProcessOpeningDirective`, `ProcessClosingDirective`)

**Opening Directives**:
- `{#if condition}` → Creates `IfNode` with condition expression
- `{#for variableName in collectionExpression}` → Creates `ForNode` with variable and collection

**Closing Directives**:
- `{/if}` → Matches with opening `{#if}`
- `{/for}` → Matches with opening `{#for}`

**Directive Stack**: Uses `_directiveStack` to match opening/closing directives, similar to element stack but separate. This allows directives to nest properly.

**Nesting**: Directives can contain other directives, elements, and code blocks. The parser maintains proper nesting by tracking directive types.

### Text Content Processing

`ProcessTextContent()` handles everything between tags and code blocks:

1. Reads all characters until encountering `<` or `{`
2. Inside script/style tags: Preserves ALL content including whitespace
3. Outside script/style tags: Filters out pure whitespace text nodes
4. Creates `TextNode` with location tracking

**Special Handling**: Inside `<script>` and `<style>` tags:
- Regular `{` characters are treated as literal text (JavaScript/CSS syntax)
- Only directives (`{#if}`, `{/if}`, etc.) are processed as code blocks
- This preserves JavaScript object literals and CSS syntax

## Special Features

### Component Syntax

Components extend HTML with a component reference syntax:

```chtml
<:Header title="Home" />
<:Layout>
  <p>Content</p>
</:Layout>
```

Components support:
- **Simple names**: `ComponentName`
- **Relative paths**: `./ComponentName` or `.ComponentName`
- **Fully qualified**: `Namespace.ComponentName`
- **Attributes**: Same as HTML elements, including code blocks
- **Children**: Components can have children that are passed to the component

### Code Blocks in Attributes

Attributes can contain code blocks:

```chtml
<div class={isActive ? "active" : "inactive"}>
<img src={imageUrl ?? "default.jpg"} />
```

The parser handles:
- Nested braces within code blocks
- Quoted strings inside code blocks
- Escaped quotes

### Directives

Directives provide conditional and iterative rendering:

```chtml
{#if user.IsLoggedIn}
  <p>Welcome, {user.Name}!</p>
{/if}

{#for item in items}
  <div>{item.Name}</div>
{/for}
```

Directives:
- Can be nested
- Can contain other directives, elements, and code blocks
- Maintain proper nesting via directive stack
- Work inside script/style tags (for conditional script/style)

### Front Matter

YAML front matter at the start of the document:

```chtml
---
title: My Page
author: John Doe
tags: [html, web]
---

<html>...</html>
```

Front matter:
- Must start with `---` on first line
- Must end with `---` on separate line
- Parsed as YAML into a dictionary
- Raw text preserved even if parsing fails

### Script/Style Hoisting

Script and style tags at the bottom of the document are "hoisted":

1. Detected when closing `</script>` or `</style>` tag
2. Checked if tag is at bottom (only whitespace/closing tags follow)
3. Converted from `ElementNode` to `ScriptNode` or `StyleNode`
4. Content extracted and stored in node
5. Can be rendered separately via `RenderScripts()` and `RenderStyles()`

This allows scripts/styles to be collected and rendered at the end of the document.

## Example Flow

For CHTML like:
```chtml
---
title: Home
---
<div>
  {#if showTitle}
    <h1>{title}</h1>
  {/if}
  <:Header />
</div>
```

The parsing flow:

1. **Front Matter**: Parse YAML, store in `_document.FrontMatter`
2. **`<div>`**: Create `ElementNode`, push onto stack, set as `_currentParent`
3. **Text "  "**: Filtered out
4. **`{#if showTitle}`**: Create `IfNode`, push onto `_directiveStack`, set as `_currentParent`
5. **Text "    "**: Filtered out
6. **`<h1>`**: Create `ElementNode`, push onto stack, set as `_currentParent`
7. **`{title}`**: Create `CodeNode`, add to `<h1>` children
8. **`</h1>`**: Pop `<h1>` from stack, restore `_currentParent` to `IfNode`
9. **`{/if}`**: Pop `IfNode` from `_directiveStack`, restore `_currentParent` to `<div>`
10. **`<:Header />`**: Create `ComponentNode`, add to `<div>` children
11. **`</div>`**: Pop `<div>` from stack, restore `_currentParent` to document

## Error Handling

The parser handles malformed CHTML gracefully:

- **Mismatched closing tags**: Pops up the stack until finding a match
- **Mismatched directives**: Pops up directive stack until finding a match
- **Unclosed tags/directives**: Remain on stack (can be detected after parsing)
- **Invalid front matter**: Raw text preserved, parsing continues
- **Invalid code blocks**: First `}` closes the block (no nesting support)

## Performance Considerations

- **Streaming**: Processes large files without loading entire content into memory
- **Buffer Pooling**: Uses `ArrayPool<byte>` for efficient buffer management
- **Single Pass**: No backtracking or multiple passes required
- **Minimal Allocations**: Reuses `StringBuilder` and buffers where possible
- **YAML Parsing**: Only parses front matter if present (checked efficiently)

## Usage

```csharp
// From stream
using var stream = new FileStream("page.chtml", FileMode.Open);
var parser = new ChtmlParser(stream);
var document = parser.Parse();

// Access front matter
var title = document.FrontMatter?["title"]?.ToString();

// From string
var document = ChtmlParser.Parse("---\ntitle: Test\n---\n<html>...</html>");

// From bytes
var bytes = Encoding.UTF8.GetBytes("...");
var document = ChtmlParser.Parse(bytes);
```

## Key Methods

- `ProcessCharacter()`: Main character routing logic
- `ProcessTag()`: Routes to opening/closing/component/special tag handlers
- `ProcessCodeBlock()`: Routes to regular code block or directive handlers
- `ProcessOpeningDirective()`: Handles `{#if}` and `{#for}` directives
- `ProcessClosingDirective()`: Handles `{/if}` and `{/for}` directives
- `ProcessComponentTag()`: Handles `<:ComponentName />` syntax
- `ParseFrontMatter()`: Parses YAML front matter at document start
- `ParseCodeBlock()`: Parses regular `{expression}` code blocks
- `ReadCodeBlockInAttribute()`: Parses code blocks in attribute values
- `IsDirectiveAtPosition()`: Checks if `{` starts a directive (for script/style handling)

## Node Types

CHTML extends HTML with additional node types:

- `ComponentNode`: Component references (`<:ComponentName />`)
- `CodeNode`: Code blocks (`{expression}`)
- `IfNode`: Conditional directive (`{#if condition}...{/if}`)
- `ForNode`: Iterative directive (`{#for item in collection}...{/for}`)
- `ScriptNode`: Hoisted script tags (bottom scripts)
- `StyleNode`: Hoisted style tags (bottom styles)

## Differences from HTML Parser

| Feature | HTML | CHTML |
|---------|------|-------|
| Components | No | Yes (`<:Name />`) |
| Code blocks | No | Yes (`{expr}`) |
| Directives | No | Yes (`{#if}`, `{#for}`) |
| Front matter | No | Yes (YAML) |
| Script/style hoisting | No | Yes (bottom tags) |
| Code in attributes | No | Yes (`attr={expr}`) |

