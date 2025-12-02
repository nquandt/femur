# Femur.Markdown.Extended.Parser

Extended Markdown parser with YAML frontmatter support.

This package extends the base Markdown parser to support YAML frontmatter, allowing metadata to be embedded at the beginning of Markdown documents.

## Features

- Full CommonMark 0.31.2 compliance (inherits from base parser)
- YAML frontmatter parsing (delimited by `---`)
- Nested and complex YAML structures
- Graceful handling of malformed YAML
- **Composable parser architecture** - demonstrates extending parsers through inheritance

## Installation

```
dotnet add package Femur.Markdown.Extended.Parser
```

## Usage

```csharp
using Femur.Markdown.Extended.Parser;
using Femur.Markdown.Extended.Abstractions.Nodes;
using System.IO;

// Parse markdown with YAML frontmatter from a stream
using (var stream = File.OpenRead("document.md"))
{
    var parser = new ExtendedMarkdownParser(stream);
    var document = parser.Parse();

    // Access frontmatter
    if (document is ExtendedMarkdownDocumentNode extendedDoc)
    {
        if (extendedDoc.FrontMatter != null)
        {
            foreach (var (key, value) in extendedDoc.FrontMatter)
            {
                Console.WriteLine($"{key}: {value}");
            }
        }
        
        // Access regular markdown content
        foreach (var child in extendedDoc.Children)
        {
            // Process markdown nodes as usual
        }
    }
}
```

## Frontmatter Format

The parser expects YAML frontmatter at the very start of the document, delimited by `---`:

```markdown
---
title: My Document
author: John Doe
tags:
  - markdown
  - yaml
date: 2024-01-01
metadata:
  version: 1.0
  published: true
---

# Document Content

This is the actual markdown content...
```

### Supported YAML Features

- Scalars (strings, numbers, booleans, dates)
- Lists
- Nested mappings (objects)

### Parsing Behavior

- If no frontmatter is present, `FrontMatter` is `null`
- If frontmatter is malformed, `FrontMatterRaw` contains the raw text (for debugging)
- The `FrontMatter` property will be `null` if YAML parsing fails
- Document parsing continues normally if frontmatter is invalid

## Architecture: Composable Parsers

ExtendedMarkdownParser demonstrates Femur's composable parser architecture:

- **Inheritance-based composition**: Extends `MarkdownParser` rather than reimplementing parsing logic
- **Preprocessing hooks**: Overrides `InitializeParsing()` to extract frontmatter as a preprocessing block
- **Shared buffer**: Uses the same StreamReader buffer for frontmatter and markdown content
- **Delegation pattern**: Calls `base.InitializeParsing()` after frontmatter extraction to perform standard markdown parsing

This pattern can be repeated for other preprocessing needs:
- Extracting directives (e.g., `<!-- directive -->`)
- Processing metadata comments
- Filtering or transforming content
- Any preprocessing that should happen before main parsing

All preprocessing shares the same buffer and maintains true streaming semantics.

## License

MIT
