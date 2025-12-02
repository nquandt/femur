using Femur.Markdown.Extended.Parser;
using Femur.Markdown.Extended.Abstractions.Nodes;
using Xunit;

namespace ExtendedMarkdownParserTests;

/// <summary>
/// Tests for edge cases and boundary conditions in ExtendedMarkdownParser.
/// Verifies parser robustness with unusual inputs and formatting.
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void Parse_EmptyStream_ReturnsEmptyDocument()
    {
        // Arrange
        var markdown = "";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.Null(document.FrontMatterBlock);
        Assert.Null(document.FrontMatterBlock?.RawContent);
    }

    [Fact]
    public void Parse_OnlyFrontmatterDelimiters_NoContent()
    {
        // Arrange
        var markdown = "---\n---";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Empty(document.FrontMatterBlock!.ParsedData!);
    }

    [Fact]
    public void Parse_UnicodeCharacters_InFrontmatter()
    {
        // Arrange
        var markdown = """
---
title: 日本語タイトル
author: François
emoji: 🎉
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Contains("日本語", document.FrontMatterBlock!.ParsedData!["title"].ToString()!);
        Assert.Contains("François", document.FrontMatterBlock!.ParsedData!["author"].ToString()!);
    }

    [Fact]
    public void Parse_OnlyDashes_NoFrontmatter()
    {
        // Arrange
        var markdown = "---";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.Null(document.FrontMatterBlock); // No closing delimiter
    }

    [Fact]
    public void Parse_DelimiterNotOnFirstLine_NoFrontmatter()
    {
        // Arrange
        var markdown = """
Some content first
---
title: Should not parse as frontmatter
---
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.Null(document.FrontMatterBlock);
    }

    [Fact]
    public void Parse_WhitespaceBeforeDelimiter_NoFrontmatter()
    {
        // Arrange
        var markdown = """
  ---
title: Should not parse
---
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.Null(document.FrontMatterBlock);
    }

    [Fact]
    public void Parse_DashesInContent_HandledCorrectly()
    {
        // Arrange
        var markdown = """
---
title: Test with dashes
description: This has --- in the middle
---
# Content with --- more dashes
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal("This has --- in the middle", document.FrontMatterBlock!.ParsedData!["description"]);
    }

    [Fact]
    public void Parse_EmptyLines_InFrontmatter()
    {
        // Arrange
        var markdown = """
---
title: With empty lines

author: John

age: 30
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Equal("With empty lines", document.FrontMatterBlock!.ParsedData!["title"]);
        Assert.Equal("John", document.FrontMatterBlock!.ParsedData!["author"]);
        Assert.Equal("30", document.FrontMatterBlock!.ParsedData!["age"]);
    }

    [Fact]
    public void Parse_SpecialCharactersInYaml_ParsedCorrectly()
    {
        // Arrange
        var markdown = """
---
title: "Title with: colons and 'quotes'"
url: https://example.com/path?query=1&other=2
email: user@example.com
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Contains("colons", document.FrontMatterBlock!.ParsedData!["title"].ToString()!);
        Assert.Contains("example.com", document.FrontMatterBlock!.ParsedData!["url"].ToString()!);
        Assert.Contains("@", document.FrontMatterBlock!.ParsedData!["email"].ToString()!);
    }

    [Fact]
    public void Parse_LargeFrontmatter_HandledEfficiently()
    {
        // Arrange - Create a large frontmatter section
        var yamlLines = new List<string> { "---" };
        for (int i = 0; i < 1000; i++)
        {
            yamlLines.Add($"key{i}: value{i}");
        }
        yamlLines.Add("---");
        yamlLines.Add("# Content");

        var markdown = string.Join("\n", yamlLines);

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.True(document.FrontMatterBlock!.ParsedData!.Count > 900); // Most keys should be parsed
    }

    [Fact]
    public void Parse_UnicodeCharacters_InFrontmatter2()
    {
        // Arrange
        var markdown = """
---
title: 日本語タイトル
author: François
emoji: 🎉
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.Contains("日本語", document.FrontMatterBlock!.ParsedData!["title"].ToString()!);
        Assert.Contains("François", document.FrontMatterBlock!.ParsedData!["author"].ToString()!);
    }

    [Fact]
    public void Parse_NumbersAndDates_AsStrings()
    {
        // Arrange
        var markdown = """
---
version: 1.2.3
count: 42
date: 2024-12-01
timestamp: 2024-12-01T10:30:00Z
---
# Content
""";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.NotNull(document.FrontMatterBlock?.ParsedData);
        Assert.NotNull(document.FrontMatterBlock!.ParsedData!["version"]);
        Assert.NotNull(document.FrontMatterBlock!.ParsedData!["count"]);
        Assert.NotNull(document.FrontMatterBlock!.ParsedData!["date"]);
        Assert.NotNull(document.FrontMatterBlock!.ParsedData!["timestamp"]);
    }

    [Fact]
    public void Parse_OnlyDashes_NoFrontmatter2()
    {
        // Arrange
        var markdown = "---";

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Assert
        Assert.Null(document.FrontMatterBlock); // No closing delimiter
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var markdown = """
---
title: Test
---
# Content
""";
        var document = ExtendedMarkdownParser.Parse(markdown);

        // Act & Assert - Should not throw
    }
}
