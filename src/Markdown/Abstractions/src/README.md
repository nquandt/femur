# Femur.Markdown.Abstractions

This library provides abstract base classes and interfaces for Markdown AST (Abstract Syntax Tree) nodes and utilities for traversing them. It defines a complete set of node types that represent the structure of Markdown documents according to the CommonMark specification.

## Overview

`Femur.Markdown.Abstractions` enables you to work with Markdown documents as a structured AST. It provides:

- **Node types** representing all major Markdown elements (blocks, inline elements, text)
- **AST Walker** for traversing and processing Markdown documents
- **Type-safe** visitor pattern for custom processing logic

## Node Types

### Block-Level Nodes

Block-level nodes represent structural elements of a Markdown document.

#### Document Node
- **Class**: `MarkdownDocumentNode`
- **Description**: The root node of a Markdown document, containing all block-level content
- **Children**: Any block-level nodes (headings, paragraphs, lists, etc.)

#### Heading Node
- **Class**: `HeadingNode`
- **Description**: Represents headings (ATX or Setext style)
- **Properties**:
  - `Level` (int): Heading level from 1 to 6
- **Children**: Inline content (text, emphasis, links, etc.)
- **Example**: `# This is a heading` (Level 1)

#### Paragraph Node
- **Class**: `ParagraphNode`
- **Description**: Represents a paragraph of text
- **Children**: Inline content (text, emphasis, links, code spans, etc.)

#### Block Quote Node
- **Class**: `BlockQuoteNode`
- **Description**: Represents a quoted block
- **Children**: Block-level nodes (paragraphs, headings, nested block quotes, etc.)

#### Code Block Node
- **Class**: `CodeBlockNode`
- **Description**: Represents a code block (indented or fenced)
- **Properties**:
  - `Content` (string): The code content
  - `Info` (string?): Language identifier for fenced code blocks
  - `IsFenced` (bool): Whether this is a fenced code block
- **Leaf Node**: Has no children
- **Example**: ` ```csharp\nvar x = 42;\n``` `

#### List Node
- **Class**: `ListNode`
- **Description**: Represents an ordered or unordered list
- **Properties**:
  - `IsOrdered` (bool): True for ordered lists, false for unordered
  - `StartNumber` (int): Starting number for ordered lists (default 1)
  - `BulletChar` (char): Marker for unordered lists ('-', '*', or '+')
  - `IsLoose` (bool): True if list items are separated by blank lines
- **Children**: `ListItemNode` instances

#### List Item Node
- **Class**: `ListItemNode`
- **Description**: Represents a single item in a list
- **Children**: Block-level nodes (paragraphs, nested lists, code blocks, etc.)

#### Thematic Break Node
- **Class**: `ThematicBreakNode`
- **Description**: Represents a horizontal rule (---, ***, ___)
- **Leaf Node**: Has no children

#### HTML Block Node
- **Class**: `HtmlBlockNode`
- **Description**: Represents raw HTML content
- **Properties**:
  - `Content` (string): The HTML content
- **Leaf Node**: Has no children

### Inline Nodes

Inline nodes represent elements that appear within block-level content.

#### Text Node
- **Class**: `MarkdownTextNode`
- **Description**: Plain text content
- **Properties**:
  - `Content` (string): The text content
- **Leaf Node**: Has no children

#### Emphasis Node
- **Class**: `EmphasisNode`
- **Description**: Represents emphasis (italic) text
- **Children**: Inline content (typically text, but can include other inline elements)
- **Example**: `*emphasized*` or `_emphasized_`

#### Strong Emphasis Node
- **Class**: `StrongEmphasisNode`
- **Description**: Represents strong emphasis (bold) text
- **Children**: Inline content
- **Example**: `**strong**` or `__strong__`

#### Link Node
- **Class**: `LinkNode`
- **Description**: Represents a hyperlink
- **Properties**:
  - `Url` (string): The target URL
  - `Title` (string?): Optional link title (displayed on hover)
- **Children**: Inline content for the link text
- **Example**: `[link text](https://example.com "title")`

#### Image Node
- **Class**: `ImageNode`
- **Description**: Represents an embedded image
- **Properties**:
  - `Url` (string): The image URL
  - `Title` (string?): Optional image title
- **Children**: Inline content for the alt text
- **Example**: `![alt text](image.png "title")`

#### Code Span Node
- **Class**: `CodeSpanNode`
- **Description**: Represents inline code
- **Properties**:
  - `Content` (string): The code content
- **Leaf Node**: Has no children
- **Example**: ` `code` `

#### Hard Line Break Node
- **Class**: `HardLineBreakNode`
- **Description**: Represents a hard line break (rendered as `<br>`)
- **Leaf Node**: Has no children

#### Soft Line Break Node
- **Class**: `SoftLineBreakNode`
- **Description**: Represents a soft line break (typically rendered as a space)
- **Leaf Node**: Has no children

## Using the AST Walker

The `MarkdownAstWalker` is an abstract base class that implements the visitor pattern for traversing Markdown AST nodes. It provides a type-safe way to process Markdown documents.

### Basic Usage

Create a subclass of `MarkdownAstWalker` and override the visitor methods for node types you want to process:

```csharp
public class MyMarkdownProcessor : MarkdownAstWalker
{
    public override void VisitHeading(HeadingNode node)
    {
        Console.WriteLine($"Found heading level {node.Level}");
        base.VisitHeading(node);  // Continue walking children
    }

    public override void VisitParagraph(ParagraphNode node)
    {
        Console.WriteLine("Found paragraph");
        base.VisitParagraph(node);
    }
}

// Usage
var walker = new MyMarkdownProcessor();
walker.Walk(documentNode);
```

### Advanced Processing

You can perform more complex operations by controlling when to walk children:

```csharp
public class CodeCollector : MarkdownAstWalker
{
    public List<string> CodeBlocks { get; } = new();

    protected override void VisitCodeBlock(CodeBlockNode node)
    {
        CodeBlocks.Add(node.Content);
        // Don't call base - code blocks have no children
    }
}

var collector = new CodeCollector();
collector.Walk(documentNode);
foreach (var code in collector.CodeBlocks)
{
    Console.WriteLine(code);
}
```

### Available Visitor Methods

The `MarkdownAstWalker` provides the following protected virtual methods that you can override:

**Block-Level Visitors:**
- `VisitDocument(MarkdownDocumentNode)`
- `VisitHeading(HeadingNode)`
- `VisitParagraph(ParagraphNode)`
- `VisitBlockQuote(BlockQuoteNode)`
- `VisitCodeBlock(CodeBlockNode)`
- `VisitList(ListNode)`
- `VisitListItem(ListItemNode)`
- `VisitThematicBreak(ThematicBreakNode)`
- `VisitHtmlBlock(HtmlBlockNode)`

**Inline Visitors:**
- `VisitEmphasis(EmphasisNode)`
- `VisitStrongEmphasis(StrongEmphasisNode)`
- `VisitLink(LinkNode)`
- `VisitImage(ImageNode)`
- `VisitCodeSpan(CodeSpanNode)`
- `VisitHardLineBreak(HardLineBreakNode)`
- `VisitSoftLineBreak(SoftLineBreakNode)`
- `VisitText(MarkdownTextNode)`

### Key Methods

- **`Walk(MarkdownDocumentNode)`**: Start traversing from the root document node
- **`WalkChildren(MarkdownContainerNode)`**: Walk all children of a container node (called automatically by visitor methods)
- **`VisitNode(Node)`**: Internal routing method that dispatches to the appropriate visitor based on node type

### Node Hierarchy

The walker uses a type hierarchy to organize nodes:

- **`MarkdownContainerNode`**: Base class for nodes that can have children
  - `MarkdownDocumentNode`
  - `HeadingNode`
  - `ParagraphNode`
  - `BlockQuoteNode`
  - `ListNode`
  - `ListItemNode`
  - `EmphasisNode`
  - `StrongEmphasisNode`
  - `LinkNode`
  - `ImageNode`

- **`MarkdownLeafNode`**: Base class for nodes that cannot have children
  - `CodeBlockNode`
  - `ThematicBreakNode`
  - `HtmlBlockNode`
  - `CodeSpanNode`
  - `HardLineBreakNode`
  - `SoftLineBreakNode`
  - `MarkdownTextNode`

## Example: Building a Table of Contents

```csharp
public class TableOfContentsBuilder : MarkdownAstWalker
{
    private List<(int Level, string Title)> Headings { get; } = new();

    protected override void VisitHeading(HeadingNode node)
    {
        // Extract text from heading children
        var textCollector = new TextCollector();
        foreach (var child in node.Children)
        {
            textCollector.VisitNode(child);
        }
        
        Headings.Add((node.Level, textCollector.Text));
        base.VisitHeading(node);
    }

    public List<(int Level, string Title)> GetToc()
    {
        return Headings;
    }
}

private class TextCollector : MarkdownAstWalker
{
    public string Text { get; private set; } = string.Empty;

    protected override void VisitText(MarkdownTextNode node)
    {
        Text += node.Content;
    }
}
```