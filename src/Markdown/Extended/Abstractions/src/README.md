# Femur.Markdown.Extended.Abstractions

Extended abstractions for Markdown parsing with YAML frontmatter support.

This package extends the base Markdown abstractions to support YAML frontmatter, allowing metadata to be embedded at the beginning of Markdown documents.

## Features

- Extended document node with frontmatter support
- YAML metadata parsing and storage
- Maintains compatibility with base Markdown node structure

## Installation

```
dotnet add package Femur.Markdown.Extended.Abstractions
```

## Usage

The extended abstractions provide access to frontmatter metadata when parsing Markdown documents with YAML frontmatter.

```csharp
using Femur.Markdown.Extended.Abstractions.Nodes;
using Femur.Markdown.Extended.Parser;

// Parse markdown with YAML frontmatter
var parser = new ExtendedMarkdownParser();
var document = parser.Parse(markdownContent);

// Access frontmatter
if (document is ExtendedMarkdownDocumentNode extendedDoc && extendedDoc.FrontMatter != null)
{
    var title = extendedDoc.FrontMatter.TryGetValue("title", out var titleObj) ? titleObj : null;
    var tags = extendedDoc.FrontMatter.TryGetValue("tags", out var tagsObj) ? tagsObj : null;
}
```

## Frontmatter Format

Frontmatter should be delimited by `---` at the start of the document:

```markdown
---
title: My Document
tags:
  - markdown
  - yaml
date: 2024-01-01
---

# Document content here
```

## License

MIT
