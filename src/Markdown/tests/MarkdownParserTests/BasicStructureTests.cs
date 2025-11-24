using Femur.Markdown.Abstractions;
using static Femur.Markdown.Abstractions.MarkdownNodeType;
using Femur.Markdown.Abstractions.Nodes;
using MarkdownParserInstance = Femur.Markdown.Parser.MarkdownParser;

namespace MarkdownParserTests;

public class BasicStructureTests : IClassFixture<TestFixture>, IDisposable
{
    public BasicStructureTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Basic Document Structure

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

    #endregion

    #region ATX Headings

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
    public void Parse_AtxHeadingLevel6_ParsesCorrectly()
    {
        var markdown = "###### Heading 6";
        var result = MarkdownParserInstance.Parse(markdown);

        var heading = Assert.IsType<HeadingNode>(result.Children[0]);
        Assert.Equal(6, heading.Level);
    }

    [Fact]
    public void Parse_AtxHeadingWithTrailingHashes_RemovesTrailingHashes()
    {
        var markdown = "# Heading #";
        var result = MarkdownParserInstance.Parse(markdown);

        var heading = Assert.IsType<HeadingNode>(result.Children[0]);
        Assert.Equal(1, heading.Level);
    }

    [Fact]
    public void Parse_MultipleAtxHeadings_ParsesAll()
    {
        var markdown = "# Heading 1\n\n## Heading 2\n\n### Heading 3";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(3, result.Children.Count);
        Assert.All(result.Children, child =>
        {
            var heading = Assert.IsType<HeadingNode>(child);
            Assert.True(heading.Level >= 1 && heading.Level <= 6);
        });
    }

    #endregion

    #region Setext Headings

    [Fact]
    public void Parse_SetextHeadingLevel1_ParsesCorrectly()
    {
        var markdown = "Heading\n===";
        var result = MarkdownParserInstance.Parse(markdown);

        var heading = Assert.IsType<HeadingNode>(result.Children[0]);
        Assert.Equal(1, heading.Level);
    }

    [Fact]
    public void Parse_SetextHeadingLevel2_ParsesCorrectly()
    {
        var markdown = "Heading\n---";
        var result = MarkdownParserInstance.Parse(markdown);

        var heading = Assert.IsType<HeadingNode>(result.Children[0]);
        Assert.Equal(2, heading.Level);
    }

    #endregion

    #region Thematic Breaks

    [Fact]
    public void Parse_ThematicBreakWithDashes_ParsesCorrectly()
    {
        var markdown = "---";
        var result = MarkdownParserInstance.Parse(markdown);

        var thematicBreak = Assert.IsType<ThematicBreakNode>(result.Children[0]);
        Assert.Equal(MarkdownNodeType.ThematicBreak, thematicBreak.NodeType);
    }

    [Fact]
    public void Parse_ThematicBreakWithAsterisks_ParsesCorrectly()
    {
        var markdown = "***";
        var result = MarkdownParserInstance.Parse(markdown);

        var thematicBreak = Assert.IsType<ThematicBreakNode>(result.Children[0]);
        Assert.Equal(MarkdownNodeType.ThematicBreak, thematicBreak.NodeType);
    }

    [Fact]
    public void Parse_ThematicBreakWithUnderscores_ParsesCorrectly()
    {
        var markdown = "___";
        var result = MarkdownParserInstance.Parse(markdown);

        var thematicBreak = Assert.IsType<ThematicBreakNode>(result.Children[0]);
        Assert.Equal(MarkdownNodeType.ThematicBreak, thematicBreak.NodeType);
    }

    #endregion

    #region Code Blocks

    [Fact]
    public void Parse_FencedCodeBlock_ParsesCorrectly()
    {
        var markdown = "```\ncode\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.Equal(MarkdownNodeType.CodeBlock, codeBlock.NodeType);
        Assert.True(codeBlock.IsFenced);
        Assert.Equal("code", codeBlock.Content.Trim());
    }

    [Fact]
    public void Parse_FencedCodeBlockWithLanguage_ParsesInfo()
    {
        var markdown = "```csharp\ncode\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.Equal("csharp", codeBlock.Info);
        Assert.True(codeBlock.IsFenced);
    }

    [Fact]
    public void Parse_IndentedCodeBlock_ParsesCorrectly()
    {
        var markdown = "    code";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.Equal(MarkdownNodeType.CodeBlock, codeBlock.NodeType);
        Assert.False(codeBlock.IsFenced);
        Assert.Equal("code", codeBlock.Content.Trim());
    }

    #endregion

    #region Block Quotes

    [Fact]
    public void Parse_BlockQuote_ParsesCorrectly()
    {
        var markdown = "> Quote";
        var result = MarkdownParserInstance.Parse(markdown);

        var blockQuote = Assert.IsType<BlockQuoteNode>(result.Children[0]);
        Assert.Equal(MarkdownNodeType.BlockQuote, blockQuote.NodeType);
        _ = Assert.Single(blockQuote.Children);
        _ = Assert.IsType<ParagraphNode>(blockQuote.Children[0]);
    }

    [Fact]
    public void Parse_MultilineBlockQuote_ParsesCorrectly()
    {
        var markdown = "> Line 1\n> Line 2";
        var result = MarkdownParserInstance.Parse(markdown);

        var blockQuote = Assert.IsType<BlockQuoteNode>(result.Children[0]);
        _ = Assert.Single(blockQuote.Children);
    }

    #endregion

    #region Lists

    [Fact]
    public void Parse_UnorderedList_ParsesCorrectly()
    {
        var markdown = "- Item 1\n- Item 2";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Equal(MarkdownNodeType.List, list.NodeType);
        Assert.False(list.IsOrdered);
        Assert.Equal('-', list.BulletChar);
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
    public void Parse_ListWithParagraphs_ParsesCorrectly()
    {
        var markdown = "- Item\n\n  Paragraph";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        var listItem = Assert.IsType<ListItemNode>(list.Children[0]);
        Assert.NotEmpty(listItem.Children);
    }

    #endregion

    #region Static Parse Methods

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

    #endregion
}

