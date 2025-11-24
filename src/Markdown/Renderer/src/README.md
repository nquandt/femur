# Femur.Parsers.Markdown.Renderer

HTML renderer for Markdown Abstract Syntax Trees.

## Overview

This package provides tools to render Markdown AST (Abstract Syntax Trees) produced by `Femur.Parsers.Markdown` to HTML. It includes a configurable HTML renderer that walks the AST and generates semantic HTML output.

## Key Classes

### MarkdownHtmlRenderer

Main renderer class that converts Markdown AST to HTML.

```csharp
var parser = new MarkdownParser(stream);
var document = parser.Parse();

var renderer = new MarkdownHtmlRenderer();
string html = renderer.Render(document);
```

#### Features

- Walks the entire Markdown AST
- Generates semantic HTML 5 output
- Handles all CommonMark block and inline elements
- Preserves semantic meaning through appropriate HTML tags

### MarkdownAstWalker

Base class for AST traversal. Provides:

- Visitor pattern implementation
- Methods for visiting each node type
- Extensibility for custom renderers

## Supported Elements

### Block-Level Elements

- Headings (h1-h6)
- Paragraphs
- Unordered/ordered lists
- List items
- Code blocks
- Block quotes
- Thematic breaks (horizontal rules)

### Inline Elements

- Emphasis (em, strong)
- Code spans
- Links
- Images
- Line breaks
- Hard breaks

### Text Processing

- Literal text
- HTML entity handling
- Special character escaping

## Architecture

### Rendering Pipeline

1. **Parse**: Create AST using `Femur.Parsers.Markdown`
2. **Walk**: `MarkdownAstWalker` traverses AST depth-first
3. **Visit**: For each node, renderer calls appropriate `Visit*` method
4. **Emit**: Each visitor method generates corresponding HTML
5. **Output**: Accumulated HTML is returned as string

### Extension Pattern

Create custom renderers by extending `MarkdownAstWalker`:

```csharp
public class CustomRenderer : MarkdownAstWalker
{
    protected override void VisitHeading(HeadingNode node)
    {
        // Custom heading rendering
    }
}
```

## Usage

### Basic HTML Rendering

```csharp
using var stream = new FileStream("document.md", FileMode.Open);
var parser = new MarkdownParser(stream);
var document = parser.Parse();

var renderer = new MarkdownHtmlRenderer();
var html = renderer.Render(document);
```

## Features

- ✅ Complete CommonMark element support
- ✅ Semantic HTML output
- ✅ Extensible visitor pattern
- ✅ Efficient AST traversal
