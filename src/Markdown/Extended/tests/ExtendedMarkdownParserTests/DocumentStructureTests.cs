using Femur.Markdown.Extended.Parser;
using Femur.Markdown.Extended.Abstractions.Nodes;
using Xunit;

namespace ExtendedMarkdownParserTests;

/// <summary>
/// Tests for document structure and markdown content parsing with frontmatter.
/// Verifies that markdown content is preserved and properly structured.
/// </summary>
public class DocumentStructureTests
{
    [Fact]
    public void Parse_CreatesExtendedDocumentNode()
    {
        // Arrange
        var markdown = """
---
title: Test
---
# Heading
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.IsType<ExtendedMarkdownDocumentNode>(document);
    }

    [Fact]
    public void Parse_PreservesMarkdownAfterFrontmatter()
    {
        // Arrange
        var markdown = """
---
title: Test Document
---
# Main Heading

Paragraph content here.

- List item 1
- List item 2
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.True(document.HasChildren, "Document should have markdown content children");
    }

    [Fact]
    public void Parse_EmptyMarkdownAfterFrontmatter_StillValid()
    {
        // Arrange
        var markdown = """
---
title: Only Frontmatter
---
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal("Only Frontmatter", document.FrontMatterBlock!.ParsedData!["title"]);
        // Document might be empty or might have default structure
        Assert.IsType<ExtendedMarkdownDocumentNode>(document);
    }

    [Fact]
    public void Parse_WithComplexMarkdown_PreservesStructure()
    {
        // Arrange
        var markdown = """
---
title: Complex
category: tutorial
---

# Introduction

This is a paragraph.

## Section 1

Some content.

```csharp
var code = "example";
```

## Section 2

More content.

- Item 1
- Item 2
  - Nested item
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal("Complex", document.FrontMatterBlock!.ParsedData!["title"]);
        Assert.Equal("tutorial", document.FrontMatterBlock!.ParsedData!["category"]);
        Assert.True(document.HasChildren);
    }

    [Fact]
    public void Parse_MultipleConsecutiveCalls_Independent()
    {
        // Arrange
        var markdown1 = """
---
id: 1
---
# Document 1
""";
        var markdown2 = """
---
id: 2
---
# Document 2
""";

        // Act
        var doc1 = ExtendedMarkdownParser.Parse(markdown1);
        var doc2 = ExtendedMarkdownParser.Parse(markdown2);

        // Assert
        Assert.Equal(1, int.Parse(doc1.FrontMatterBlock!.ParsedData!["id"].ToString()!));
        Assert.Equal(2, int.Parse(doc2.FrontMatterBlock!.ParsedData!["id"].ToString()!));
    }

    [Fact]
    public void Parse_WithWindowsLineEndings_ParsesCorrectly()
    {
        // Arrange
        var markdown = "---\r\ntitle: Windows\r\n---\r\n# Content";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal("Windows", document.FrontMatterBlock!.ParsedData!["title"]);
    }

    [Fact]
    public void Parse_WithUnixLineEndings_ParsesCorrectly()
    {
        // Arrange
        var markdown = "---\ntitle: Unix\n---\n# Content";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal("Unix", document.FrontMatterBlock!.ParsedData!["title"]);
    }

    [Fact]
    public void Parse_WithMixedLineEndings_ParsesCorrectly()
    {
        // Arrange
        var markdown = "---\r\ntitle: Mixed\n---\r\n# Content";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal("Mixed", document.FrontMatterBlock!.ParsedData!["title"]);
    }
}
