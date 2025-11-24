using Femur.Markdown.Abstractions;
using Femur.Markdown.Abstractions.Nodes;
using MarkdownParserInstance = Femur.Markdown.Parser.MarkdownParser;

namespace MarkdownParserTests;

/// <summary>
/// Tests based on CommonMark 0.31.2 specification examples
/// </summary>
public class CommonMarkComplianceTests : IClassFixture<TestFixture>, IDisposable
{
    public CommonMarkComplianceTests(TestFixture fixture)
    {
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region CommonMark Spec Examples

    [Fact]
    public void Parse_CommonMarkExample1_AtxHeadings()
    {
        // CommonMark spec example: ATX headings
        var markdown = "# foo\n## foo\n### foo\n#### foo\n##### foo\n###### foo";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(6, result.Children.Count);
        for (int i = 0; i < 6; i++)
        {
            var heading = Assert.IsType<HeadingNode>(result.Children[i]);
            Assert.Equal(i + 1, heading.Level);
        }
    }

    [Fact]
    public void Parse_CommonMarkExample2_SetextHeadings()
    {
        // CommonMark spec example: Setext headings
        var markdown = "Foo *bar*\n=========\n\nFoo *bar*\n---------";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(2, result.Children.Count);
        var heading1 = Assert.IsType<HeadingNode>(result.Children[0]);
        Assert.Equal(1, heading1.Level);
        var heading2 = Assert.IsType<HeadingNode>(result.Children[1]);
        Assert.Equal(2, heading2.Level);
    }

    [Fact]
    public void Parse_CommonMarkExample3_ThematicBreaks()
    {
        // CommonMark spec example: Thematic breaks
        var markdown = "***\n---\n___";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(3, result.Children.Count);
        Assert.All(result.Children, child => Assert.IsType<ThematicBreakNode>(child));
    }

    [Fact]
    public void Parse_CommonMarkExample4_FencedCodeBlocks()
    {
        // CommonMark spec example: Fenced code blocks
        var markdown = "```\n<\n >\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.True(codeBlock.IsFenced);
        Assert.Contains("<", codeBlock.Content);
        Assert.Contains(">", codeBlock.Content);
    }

    [Fact]
    public void Parse_CommonMarkExample5_IndentedCodeBlocks()
    {
        // CommonMark spec example: Indented code blocks
        var markdown = "    a simple\n      indented code block";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.False(codeBlock.IsFenced);
    }

    [Fact]
    public void Parse_CommonMarkExample6_BlockQuotes()
    {
        // CommonMark spec example: Block quotes
        var markdown = "> # Foo\n> bar\n> baz";
        var result = MarkdownParserInstance.Parse(markdown);

        var blockQuote = Assert.IsType<BlockQuoteNode>(result.Children[0]);
        Assert.NotEmpty(blockQuote.Children);
    }

    [Fact]
    public void Parse_CommonMarkExample7_Lists()
    {
        // CommonMark spec example: Lists
        var markdown = "- one\n- two\n- three";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.False(list.IsOrdered);
        Assert.Equal(3, list.Children.Count);
    }

    [Fact]
    public void Parse_CommonMarkExample8_OrderedLists()
    {
        // CommonMark spec example: Ordered lists
        var markdown = "1. one\n2. two\n3. three";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.True(list.IsOrdered);
        Assert.Equal(3, list.Children.Count);
    }

    [Fact]
    public void Parse_CommonMarkExample9_CodeSpans()
    {
        // CommonMark spec example: Code spans
        var markdown = "`<http://foo.bar.baz>`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Contains("http://foo.bar.baz", codeSpan.Content);
    }

    [Fact]
    public void Parse_CommonMarkExample10_Emphasis()
    {
        // CommonMark spec example: Emphasis
        var markdown = "*foo bar*";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var emphasis = Assert.IsType<EmphasisNode>(paragraph.Children[0]);
        Assert.Equal(MarkdownNodeType.Emphasis, emphasis.NodeType);
    }

    [Fact]
    public void Parse_CommonMarkExample11_StrongEmphasis()
    {
        // CommonMark spec example: Strong emphasis
        var markdown = "**foo bar**";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var strong = Assert.IsType<StrongEmphasisNode>(paragraph.Children[0]);
        Assert.Equal(MarkdownNodeType.StrongEmphasis, strong.NodeType);
    }

    [Fact]
    public void Parse_CommonMarkExample12_Links()
    {
        // CommonMark spec example: Links
        var markdown = "[link](/uri \"title\")";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var link = Assert.IsType<LinkNode>(paragraph.Children[0]);
        Assert.Equal("/uri", link.Url);
    }

    [Fact]
    public void Parse_CommonMarkExample13_Images()
    {
        // CommonMark spec example: Images
        var markdown = "![foo](/url \"title\")";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var image = Assert.IsType<ImageNode>(paragraph.Children[0]);
        Assert.Equal("/url", image.Url);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_EmptyLinesBetweenBlocks_ParsesCorrectly()
    {
        var markdown = "# Heading\n\nParagraph";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(2, result.Children.Count);
        _ = Assert.IsType<HeadingNode>(result.Children[0]);
        _ = Assert.IsType<ParagraphNode>(result.Children[1]);
    }

    [Fact]
    public void Parse_NestedLists_ParsesCorrectly()
    {
        var markdown = "- Item 1\n  - Nested 1\n  - Nested 2\n- Item 2";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Equal(2, list.Children.Count);
    }

    [Fact]
    public void Parse_LinkReferenceDefinition_ParsesCorrectly()
    {
        var markdown = "[ref]: http://example.com\n\n[link][ref]";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var link = Assert.IsType<LinkNode>(paragraph.Children[0]);
        Assert.Equal("http://example.com", link.Url);
    }

    #endregion
}

