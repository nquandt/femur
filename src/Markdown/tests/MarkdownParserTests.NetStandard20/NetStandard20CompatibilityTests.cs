using Femur.Markdown.Abstractions;
using static Femur.Markdown.Abstractions.MarkdownNodeType;
using Femur.Markdown.Abstractions.Nodes;
using MarkdownParserInstance = Femur.Markdown.Parser.MarkdownParser;

namespace MarkdownParserTests.NetStandard20;

/// <summary>
/// Tests to verify netstandard2.0 compatibility for MarkdownParser.
/// These tests cover the most common code paths and usage scenarios.
/// </summary>
public class NetStandard20CompatibilityTests
{
    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptyDocument()
    {
        var result = MarkdownParserInstance.Parse("");

        Assert.NotNull(result);
        Assert.Equal(Document, result.NodeType);
        Assert.Empty(result.Children);
    }

    [Fact]
    public void Parse_SimpleParagraph_ReturnsParagraph()
    {
        var markdown = "This is a paragraph.";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        _ = Assert.Single(result.Children);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Equal(MarkdownNodeType.Paragraph, paragraph.NodeType);
    }

    [Fact]
    public void Parse_MultipleParagraphs_ReturnsMultipleParagraphs()
    {
        var markdown = "First paragraph.\n\nSecond paragraph.";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(2, result.Children.Count);
        Assert.All(result.Children, child => Assert.IsType<ParagraphNode>(child));
    }

    [Fact]
    public void Parse_AtxHeadingLevel1_ParsesCorrectly()
    {
        var markdown = "# Heading 1";
        var result = MarkdownParserInstance.Parse(markdown);

        var heading = Assert.IsType<HeadingNode>(result.Children[0]);
        Assert.Equal(1, heading.Level);
        Assert.Equal(MarkdownNodeType.Heading, heading.NodeType);
    }

    [Fact]
    public void Parse_FencedCodeBlock_ParsesCorrectly()
    {
        var markdown = "```\ncode\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.Equal(MarkdownNodeType.CodeBlock, codeBlock.NodeType);
        Assert.True(codeBlock.IsFenced);
    }

    [Fact]
    public void Parse_UnorderedList_ParsesCorrectly()
    {
        var markdown = "- Item 1\n- Item 2";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Equal(MarkdownNodeType.List, list.NodeType);
        Assert.False(list.IsOrdered);
        Assert.Equal(2, list.Children.Count);
    }

    [Fact]
    public void Parse_OrderedList_ParsesCorrectly()
    {
        var markdown = "1. Item 1\n2. Item 2";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.True(list.IsOrdered);
        Assert.Equal(1, list.StartNumber);
        Assert.Equal(2, list.Children.Count);
    }

    [Fact]
    public void Parse_StringOverload_ParsesCorrectly()
    {
        var markdown = "# Heading";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        _ = Assert.Single(result.Children);
        _ = Assert.IsType<HeadingNode>(result.Children[0]);
    }

    [Fact]
    public void Parse_ByteArrayOverload_ParsesCorrectly()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("# Heading");
        var result = MarkdownParserInstance.Parse(bytes);

        Assert.NotNull(result);
        _ = Assert.Single(result.Children);
        _ = Assert.IsType<HeadingNode>(result.Children[0]);
    }
}

