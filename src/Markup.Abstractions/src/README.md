# Femur.Parsers.Markup.Abstractions

Shared abstractions for markup-based parsers (HTML, XML, CHTML).

## Overview

This package provides common abstractions and interfaces for markup parsers. It defines the node hierarchy and types used by HTML-like and XML-like parsers in the Femur parser family.

## Key Classes

### MarkupNodeType

Enumeration of node types common to markup-based formats:

- **Document**: Root node containing the entire document
- **Element**: Opening/closing tag pairs (e.g., `<div>...</div>`)
- **Text**: Text content between tags
- **Attribute**: Attributes on elements (e.g., `class="value"`)
- **Comment**: HTML/XML comments
- **CDATA**: Character data sections (XML)
- **ProcessingInstruction**: XML processing instructions
- **DocumentType**: DOCTYPE declarations

### Node Hierarchy

The following abstract node types provide the base structure:

- **ContainerNode**: Nodes that can have children (documents, elements)
- **LeafNode**: Nodes that cannot have children (text, comments, CDATA)
- **ElementNode**: Represents markup elements with:
  - Tag name
  - Attributes collection
  - Child nodes
  - Self-closing semantics

### Node Attributes

Each node provides:

- **NodeType**: Type of the node
- **SourceLocation**: Start position and length in source
- **Parent**: Reference to parent node (for tree navigation)
- **Children**: Collection of child nodes (for container nodes)

## Usage

This package is used by:

- `Femur.Parsers.HtmlParser` - HTML parsing
- `Femur.Parsers.XmlParser` - XML parsing
- `Femur.Parsers.ChtmlParser` - Component HTML parsing

## Features

- ✅ Consistent node hierarchy for markup formats
- ✅ Type-safe node collections
- ✅ Full source location tracking
- ✅ Support for various markup constructs
