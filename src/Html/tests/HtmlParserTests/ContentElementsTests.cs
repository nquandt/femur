using Femur.Markup.Abstractions.Nodes;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

public class ContentElementsTests : IClassFixture<TestFixture>, IDisposable
{
    public ContentElementsTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }
    #region Text Formatting Elements

    [Fact]
    public void Parse_Paragraph_ParsesCorrectly()
    {
        var html = "<p>This is a paragraph.</p>";
        var result = HtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("p", p.TagName, ignoreCase: true);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.Equal("This is a paragraph.", text.Content);
    }

    [Fact]
    public void Parse_LineBreak_ParsesAsVoidElement()
    {
        var html = "<p>Line 1<br>Line 2</p>";
        var result = HtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal(3, p.Children.Count);

        var br = Assert.IsType<ElementNode>(p.Children[1]);
        Assert.Equal("br", br.TagName, ignoreCase: true);
        Assert.True(br.IsVoidElement);
    }

    [Fact]
    public void Parse_HorizontalRule_ParsesAsVoidElement()
    {
        var html = "<hr>";
        var result = HtmlParserInstance.Parse(html);

        var hr = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("hr", hr.TagName, ignoreCase: true);
        Assert.True(hr.IsVoidElement);
    }

    [Fact]
    public void Parse_Emphasis_ParsesCorrectly()
    {
        var html = "<p>This is <em>emphasized</em> text.</p>";
        var result = HtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal(3, p.Children.Count);

        var em = Assert.IsType<ElementNode>(p.Children[1]);
        Assert.Equal("em", em.TagName, ignoreCase: true);
        var emText = Assert.IsType<TextNode>(em.Children[0]);
        Assert.Equal("emphasized", emText.Content);
    }

    [Fact]
    public void Parse_Strong_ParsesCorrectly()
    {
        var html = "<p>This is <strong>strong</strong> text.</p>";
        var result = HtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(result.Children[0]);
        var strong = Assert.IsType<ElementNode>(p.Children[1]);
        Assert.Equal("strong", strong.TagName, ignoreCase: true);
    }

    [Fact]
    public void Parse_Code_ParsesCorrectly()
    {
        var html = "<p>Use <code>printf()</code> to print.</p>";
        var result = HtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(result.Children[0]);
        var code = Assert.IsType<ElementNode>(p.Children[1]);
        Assert.Equal("code", code.TagName, ignoreCase: true);
    }

    [Fact]
    public void Parse_CodeWithHtmlEntities_PreservesEntitiesAsRawText()
    {
        // HTML-encoded content inside <code> must be preserved as-is (not decoded).
        // This is the contract the nquandtcom-chtml CodeGenerator relies on:
        // it reads the TextNode.Content and emits it verbatim into writer.Write().
        var html = "<code>&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;</code>";
        var result = HtmlParserInstance.Parse(html);

        var code = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("code", code.TagName, ignoreCase: true);
        var text = Assert.IsType<TextNode>(code.Children[0]);
        Assert.Contains("&lt;", text.Content);
        Assert.Contains("&gt;", text.Content);
        Assert.DoesNotContain("<script>", text.Content);
    }

    [Fact]
    public void Parse_CodeWithAmpersandEntity_PreservesAmpersandAsRawText()
    {
        var html = "<code>a &amp;&amp; b</code>";
        var result = HtmlParserInstance.Parse(html);

        var code = Assert.IsType<ElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(code.Children[0]);
        Assert.Contains("&amp;", text.Content);
        Assert.DoesNotContain("&&", text.Content);
    }

    [Fact]
    public void Parse_PreformattedText_PreservesWhitespace()
    {
        var html = "<pre>Line 1\nLine 2\n  Indented</pre>";
        var result = HtmlParserInstance.Parse(html);

        var pre = Assert.IsType<ElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(pre.Children[0]);
        Assert.Contains("\n", text.Content);
        Assert.Contains("  Indented", text.Content);
    }

    [Fact]
    public void Parse_Blockquote_ParsesCorrectly()
    {
        var html = "<blockquote>This is a quote.</blockquote>";
        var result = HtmlParserInstance.Parse(html);

        var blockquote = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("blockquote", blockquote.TagName, ignoreCase: true);
    }

    #endregion

    #region Lists

    [Fact]
    public void Parse_UnorderedList_ParsesCorrectly()
    {
        var html = "<ul><li>Item 1</li><li>Item 2</li></ul>";
        var result = HtmlParserInstance.Parse(html);

        var ul = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("ul", ul.TagName, ignoreCase: true);
        Assert.Equal(2, ul.Children.Count);

        var li1 = Assert.IsType<ElementNode>(ul.Children[0]);
        Assert.Equal("li", li1.TagName, ignoreCase: true);
    }

    [Fact]
    public void Parse_OrderedList_ParsesCorrectly()
    {
        var html = "<ol><li>First</li><li>Second</li></ol>";
        var result = HtmlParserInstance.Parse(html);

        var ol = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("ol", ol.TagName, ignoreCase: true);
        Assert.Equal(2, ol.Children.Count);
    }

    [Fact]
    public void Parse_DefinitionList_ParsesCorrectly()
    {
        var html = "<dl><dt>Term</dt><dd>Definition</dd></dl>";
        var result = HtmlParserInstance.Parse(html);

        var dl = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("dl", dl.TagName, ignoreCase: true);
        Assert.Equal(2, dl.Children.Count);

        var dt = Assert.IsType<ElementNode>(dl.Children[0]);
        Assert.Equal("dt", dt.TagName, ignoreCase: true);

        var dd = Assert.IsType<ElementNode>(dl.Children[1]);
        Assert.Equal("dd", dd.TagName, ignoreCase: true);
    }

    [Fact]
    public void Parse_NestedLists_ParsesCorrectly()
    {
        var html = "<ul><li>Item 1<ul><li>Nested 1</li></ul></li></ul>";
        var result = HtmlParserInstance.Parse(html);

        var ul = Assert.IsType<ElementNode>(result.Children[0]);
        var li = Assert.IsType<ElementNode>(ul.Children[0]);
        Assert.Equal(2, li.Children.Count);

        var nestedUl = Assert.IsType<ElementNode>(li.Children[1]);
        Assert.Equal("ul", nestedUl.TagName, ignoreCase: true);
    }

    #endregion

    #region Links and Anchors

    [Fact]
    public void Parse_AnchorWithHref_ParsesCorrectly()
    {
        var html = "<a href=\"http://example.com\">Link</a>";
        var result = HtmlParserInstance.Parse(html);

        var a = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("a", a.TagName, ignoreCase: true);
        Assert.Equal("http://example.com", a.Attributes["href"]);

        var text = Assert.IsType<TextNode>(a.Children[0]);
        Assert.Equal("Link", text.Content);
    }

    [Fact]
    public void Parse_AnchorWithName_ParsesCorrectly()
    {
        var html = "<a name=\"section1\">Section</a>";
        var result = HtmlParserInstance.Parse(html);

        var a = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("section1", a.Attributes["name"]);
    }

    [Fact]
    public void Parse_AnchorWithBothHrefAndName_ParsesBoth()
    {
        var html = "<a href=\"#top\" name=\"top\">Top</a>";
        var result = HtmlParserInstance.Parse(html);

        var a = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("#top", a.Attributes["href"]);
        Assert.Equal("top", a.Attributes["name"]);
    }

    #endregion

    #region Images

    [Fact]
    public void Parse_ImageWithSrc_ParsesAsVoidElement()
    {
        var html = "<img src=\"image.jpg\">";
        var result = HtmlParserInstance.Parse(html);

        var img = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("img", img.TagName, ignoreCase: true);
        Assert.True(img.IsVoidElement);
        Assert.Equal("image.jpg", img.Attributes["src"]);
    }

    [Fact]
    public void Parse_ImageWithAlt_ParsesAltAttribute()
    {
        var html = "<img src=\"photo.jpg\" alt=\"A photo\">";
        var result = HtmlParserInstance.Parse(html);

        var img = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("A photo", img.Attributes["alt"]);
    }

    [Fact]
    public void Parse_ImageSelfClosing_ParsesCorrectly()
    {
        var html = "<img src=\"test.jpg\" />";
        var result = HtmlParserInstance.Parse(html);

        var img = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.True(img.IsSelfClosing);
        Assert.True(img.IsVoidElement);
    }

    #endregion
}
