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

    [Fact]
    public void Parse_CodeSpan_WithLessThan_ContentIsRawUnescaped()
    {
        // The parser must store raw text; HTML encoding is the renderer's responsibility.
        var markdown = "`if (x < 5)`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Equal("if (x < 5)", codeSpan.Content);
        Assert.DoesNotContain("&lt;", codeSpan.Content);
    }

    [Fact]
    public void Parse_CodeSpan_WithGreaterThan_ContentIsRawUnescaped()
    {
        var markdown = "`x > 0`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Equal("x > 0", codeSpan.Content);
        Assert.DoesNotContain("&gt;", codeSpan.Content);
    }

    [Fact]
    public void Parse_CodeSpan_WithAmpersand_ContentIsRawUnescaped()
    {
        var markdown = "`x && y`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Equal("x && y", codeSpan.Content);
        Assert.DoesNotContain("&amp;", codeSpan.Content);
    }

    [Fact]
    public void Parse_CodeSpan_WithScriptTag_ContentIsRawUnescaped()
    {
        // This is the scenario that triggered the nquandtcom-chtml bug fix.
        // The parser must NOT HTML-encode the content — only the renderer does.
        var markdown = "`<script>`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Equal("<script>", codeSpan.Content);
        Assert.DoesNotContain("&lt;", codeSpan.Content);
        Assert.DoesNotContain("&gt;", codeSpan.Content);
    }

    [Fact]
    public void Parse_CodeSpan_WithStyleTag_ContentIsRawUnescaped()
    {
        var markdown = "`<style>body{}</style>`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Equal("<style>body{}</style>", codeSpan.Content);
        Assert.DoesNotContain("&lt;", codeSpan.Content);
    }

    [Fact]
    public void Parse_CodeSpan_WithAngleBracketsAndAmpersand_ContentIsRawUnescaped()
    {
        var markdown = "`x < 5 && y > 10`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Equal("x < 5 && y > 10", codeSpan.Content);
        Assert.DoesNotContain("&lt;", codeSpan.Content);
        Assert.DoesNotContain("&amp;", codeSpan.Content);
    }

    [Fact]
    public void Parse_CodeSpan_WithPairedHtmlTag_ContentIsRawUnescaped()
    {
        // A paired open+close HTML tag with inner text: the parser must store it verbatim.
        var markdown = "`<b>bold</b>`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Equal("<b>bold</b>", codeSpan.Content);
        Assert.DoesNotContain("&lt;", codeSpan.Content);
        Assert.DoesNotContain("&gt;", codeSpan.Content);
    }

    [Fact]
    public void Parse_CodeSpan_WithAttributedHtmlTag_ContentIsRawUnescaped()
    {
        // A tag with an attribute whose value contains a quote and ampersand.
        var markdown = "`<a href=\"x&y\">link</a>`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Equal("<a href=\"x&y\">link</a>", codeSpan.Content);
        Assert.DoesNotContain("&lt;", codeSpan.Content);
        Assert.DoesNotContain("&amp;", codeSpan.Content);
        Assert.DoesNotContain("&quot;", codeSpan.Content);
    }

    [Fact]
    public void Parse_CodeSpan_WithNestedHtmlTags_ContentIsRawUnescaped()
    {
        // Nested tags — the whole thing is raw text in the AST.
        var markdown = "`<em><strong>text</strong></em>`";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var codeSpan = Assert.IsType<CodeSpanNode>(paragraph.Children[0]);
        Assert.Equal("<em><strong>text</strong></em>", codeSpan.Content);
        Assert.DoesNotContain("&lt;", codeSpan.Content);
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

    #region Delimiter Stack - Complex Emphasis Nesting

    [Fact]
    public void Parse_TripleEmphasis_RendersAsStrongWithEmphasis()
    {
        // ***text*** should parse as <strong><em>text</em></strong>
        var markdown = "***bold and italic***";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var outer = Assert.IsType<StrongEmphasisNode>(paragraph.Children[0]);

        // Should contain emphasis node
        var inner = Assert.IsType<EmphasisNode>(outer.Children[0]);
        Assert.NotEmpty(inner.Children);
    }

    [Fact]
    public void Parse_TripleUnderscores_RendersAsStrongWithEmphasis()
    {
        // ___text___ should parse as <strong><em>text</em></strong>
        var markdown = "___bold and italic___";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var outer = Assert.IsType<StrongEmphasisNode>(paragraph.Children[0]);
        var inner = Assert.IsType<EmphasisNode>(outer.Children[0]);
        Assert.NotEmpty(inner.Children);
    }

    [Fact]
    public void Parse_NestedEmphasisBothMarkers_ParsesCorrectly()
    {
        // **foo *bar* baz** - strong containing mixed text and emphasis
        var markdown = "**foo *bar* baz**";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var strong = Assert.IsType<StrongEmphasisNode>(paragraph.Children[0]);

        // Should contain multiple children: text "foo ", emphasis with "bar", text " baz"
        Assert.True(strong.Children.Count >= 3);
    }

    [Fact]
    public void Parse_NestedEmphasisReverse_ParsesCorrectly()
    {
        // *foo **bar** baz* - can't mix *, need to use _ with *
        // So test: *foo **bar** baz* should at least parse the outer emphasis
        var markdown = "*foo **bar** baz*";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var emphasis = Assert.IsType<EmphasisNode>(paragraph.Children[0]);

        // Should have content inside emphasis
        Assert.NotEmpty(emphasis.Children);
    }

    [Fact]
    public void Parse_MultipleEmphasisSameText_ParsesCorrectly()
    {
        // This is *emphasized* and **strong** text.
        var markdown = "This is *emphasized* and **strong** text.";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);

        // Should have: text, emphasis, text, strong, text
        var hasEmphasis = paragraph.Children.OfType<EmphasisNode>().Any();
        var hasStrong = paragraph.Children.OfType<StrongEmphasisNode>().Any();

        Assert.True(hasEmphasis);
        Assert.True(hasStrong);
    }

    [Fact]
    public void Parse_EmphasisWithCode_ParsesCorrectly()
    {
        // *emphasis with `code` inside*
        var markdown = "*emphasis with `code` inside*";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var emphasis = Assert.IsType<EmphasisNode>(paragraph.Children[0]);

        // Should have text, code span, and text inside
        var hasCodeSpan = emphasis.Children.OfType<CodeSpanNode>().Any();
        Assert.True(hasCodeSpan);
    }

    [Fact]
    public void Parse_StrongWithEmphasis_ParsesCorrectly()
    {
        // **strong with *emphasis* inside**
        var markdown = "**strong with *emphasis* inside**";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var strong = Assert.IsType<StrongEmphasisNode>(paragraph.Children[0]);

        // Should contain emphasis node
        var hasEmphasis = strong.Children.OfType<EmphasisNode>().Any();
        Assert.True(hasEmphasis);
    }

    [Fact]
    public void Parse_EmphasisAtBoundaries_ParsesCorrectly()
    {
        // *start* middle **end**
        var markdown = "*start* middle **end**";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var emphasis = paragraph.Children.OfType<EmphasisNode>().FirstOrDefault();
        var strong = paragraph.Children.OfType<StrongEmphasisNode>().FirstOrDefault();

        Assert.NotNull(emphasis);
        Assert.NotNull(strong);
    }

    [Fact]
    public void Parse_MixedMarkers_StarAndUnderscore_ParsesCorrectly()
    {
        // *emphasis with __strong__ inside*
        var markdown = "*emphasis with __strong__ inside*";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var emphasis = Assert.IsType<EmphasisNode>(paragraph.Children[0]);

        var hasStrong = emphasis.Children.OfType<StrongEmphasisNode>().Any();
        Assert.True(hasStrong);
    }

    [Fact]
    public void Parse_DeeplyNestedEmphasis_ParsesCorrectly()
    {
        // **outer *middle __inner__ middle* outer**
        var markdown = "**outer *middle __inner__ middle* outer**";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var outer = Assert.IsType<StrongEmphasisNode>(paragraph.Children[0]);

        // Should have children
        Assert.NotEmpty(outer.Children);
    }

    #endregion
}
