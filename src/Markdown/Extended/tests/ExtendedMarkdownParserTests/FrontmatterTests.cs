using Femur.Markdown.Extended.Parser;
using Xunit;

namespace ExtendedMarkdownParserTests;

/// <summary>
/// Tests for YAML frontmatter parsing in ExtendedMarkdownParser.
/// Verifies frontmatter extraction, parsing, and document structure.
/// </summary>
public class FrontmatterTests
{
    [Fact]
    public void Parse_WithSimpleFrontmatter_ExtractsFrontmatter()
    {
        // Arrange
        var markdown = """
---
title: Test Document
author: John Doe
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal(2, document.FrontMatterBlock!.ParsedData!.Count);
        Assert.Equal("Test Document", document.FrontMatterBlock!.ParsedData!["title"]);
        Assert.Equal("John Doe", document.FrontMatterBlock!.ParsedData!["author"]);
    }

    [Fact]
    public void Parse_WithoutFrontmatter_ReturnsFrontmatterNull()
    {
        // Arrange
        var markdown = """
# Just Markdown
No frontmatter here
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.Null(document.FrontMatterBlock);
        Assert.Null(document.FrontMatterBlock?.RawContent);
    }

    [Fact]
    public void Parse_WithMissingClosingDelimiter_TreatsAsNoFrontmatter()
    {
        // Arrange
        var markdown = """
---
title: Incomplete frontmatter

# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        // Without closing delimiter, should treat first line as content, not frontmatter
        Assert.Null(document.FrontMatterBlock);
    }

    [Fact]
    public void Parse_WithComplexYaml_ParsesNestedStructures()
    {
        // Arrange
        var markdown = """
---
title: Complex Document
metadata:
  version: 1.0
  published: true
tags:
  - markdown
  - yaml
  - parsing
---
# Document Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal("Complex Document", document.FrontMatterBlock!.ParsedData!["title"]);

        // Check nested dictionary
        Assert.IsType<Dictionary<string, object>>(document.FrontMatterBlock!.ParsedData!["metadata"]);
        var metadata = (Dictionary<string, object>)document.FrontMatterBlock!.ParsedData!["metadata"];
        Assert.Equal("1.0", metadata["version"]);
        Assert.Equal("true", metadata["published"]);

        // Check list
        Assert.IsType<List<object>>(document.FrontMatterBlock!.ParsedData!["tags"]);
        var tags = (List<object>)document.FrontMatterBlock!.ParsedData!["tags"];
        Assert.Equal(3, tags.Count);
        Assert.Equal("markdown", tags[0]);
    }

    [Fact]
    public void Parse_WithEmptyFrontmatter_ParsesEmpty()
    {
        // Arrange
        var markdown = """
---
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Empty(document.FrontMatterBlock!.ParsedData!);
    }

    [Fact]
    public void Parse_WithInvalidYaml_StorRawTextButNoParsedData()
    {
        // Arrange
        var markdown = """
---
invalid: yaml: syntax: here
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock);
        Assert.NotNull(document.FrontMatterBlock.RawContent);
        Assert.Null(document.FrontMatterBlock.ParsedData); // Parsing failed but raw is preserved
    }

    [Fact]
    public void Parse_StoresFrontmatterRaw()
    {
        // Arrange
        var rawYaml = "title: Test\nauthor: John";
        var markdown = $"""
---
{rawYaml}
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.RawContent);
        Assert.Contains("title: Test", document.FrontMatterBlock!.RawContent!);
        Assert.Contains("author: John", document.FrontMatterBlock!.RawContent!);
    }

    [Fact]
    public void Parse_WithBooleanValues_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
published: true
draft: false
featured: yes
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        // YAML parses true/false/yes as strings in YamlDotNet by default
        Assert.Equal("true", document.FrontMatterBlock!.ParsedData!["published"]);
        Assert.Equal("false", document.FrontMatterBlock!.ParsedData!["draft"]);
        Assert.Equal("yes", document.FrontMatterBlock!.ParsedData!["featured"]);
    }

    [Fact]
    public void Parse_WithMultilineStrings_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
---
description: |
  This is a multiline
  description that spans
  multiple lines
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        var description = document.FrontMatterBlock!.ParsedData!["description"];
        Assert.NotNull(description);
        Assert.Contains("multiline", description.ToString());
    }
}
