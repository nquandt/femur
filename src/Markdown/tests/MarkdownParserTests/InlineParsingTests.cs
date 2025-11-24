using Femur.Markdown.Abstractions;
using Femur.Markdown.Abstractions.Nodes;
using MarkdownParserInstance = Femur.Markdown.Parser.MarkdownParser;

namespace MarkdownParserTests;

public class InlineParsingTests : IClassFixture<TestFixture>, IDisposable
{
    public InlineParsingTests(TestFixture fixture)
    {
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Emphasis

    [Fact]
    public void Parse_EmphasisWithAsterisk_ParsesCorrectly()
    {
        var markdown = "*emphasis*";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var emphasis = Assert.IsType<EmphasisNode>(paragraph.Children[0]);
        Assert.Equal(MarkdownNodeType.Emphasis, emphasis.NodeType);
    }

    [Fact]
    public void Parse_EmphasisWithUnderscore_ParsesCorrectly()
    {
        var markdown = "_emphasis_";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var emphasis = Assert.IsType<EmphasisNode>(paragraph.Children[0]);
        Assert.Equal(MarkdownNodeType.Emphasis, emphasis.NodeType);
    }

    [Fact]
    public void Parse_StrongEmphasis_ParsesCorrectly()
    {
        var markdown = "**strong**";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var strong = Assert.IsType<StrongEmphasisNode>(paragraph.Children[0]);
        Assert.Equal(MarkdownNodeType.StrongEmphasis, strong.NodeType);
    }

    #endregion

    #region Code Spans

    [Fact]
    public void Parse_CodeSpan_ParsesCorrectly()
    {
        var markdown = "`code`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Equal(MarkdownNodeType.CodeSpan, codeSpan.NodeType);
        Assert.Equal("code", codeSpan.Content);
    }

    #endregion

    #region Links

    [Fact]
    public void Parse_InlineLink_ParsesCorrectly()
    {
        var markdown = "[text](url)";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var link = Assert.IsType<LinkNode>(paragraph.Children[0]);
        Assert.Equal(MarkdownNodeType.Link, link.NodeType);
        Assert.Equal("url", link.Url);
    }

    [Fact]
    public void Parse_ReferenceLink_ParsesCorrectly()
    {
        var markdown = "[text][ref]\n\n[ref]: http://example.com";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var link = Assert.IsType<LinkNode>(paragraph.Children[0]);
        Assert.Equal("http://example.com", link.Url);
    }

    #endregion

    #region Images

    [Fact]
    public void Parse_Image_ParsesCorrectly()
    {
        var markdown = "![alt](url)";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var image = Assert.IsType<ImageNode>(paragraph.Children[0]);
        Assert.Equal(MarkdownNodeType.Image, image.NodeType);
        Assert.Equal("url", image.Url);
    }

    #endregion

    #region Mixed Inline Content

    [Fact]
    public void Parse_MixedInlineContent_ParsesCorrectly()
    {
        var markdown = "Text with *emphasis* and `code`.";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.True(paragraph.Children.Count >= 3); // Text, emphasis, text, code, text
    }

    #endregion
}

