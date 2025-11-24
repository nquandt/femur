using Femur.Markup.Abstractions.Nodes;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

public class AttributesTests : IClassFixture<TestFixture>, IDisposable
{
    public AttributesTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }
    #region Attributes

    [Fact]
    public void Parse_QuotedAttribute_ParsesCorrectly()
    {
        var html = "<div id=\"my-id\" class=\"my-class\">Content</div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("my-id", div.Attributes["id"]);
        Assert.Equal("my-class", div.Attributes["class"]);
    }

    [Fact]
    public void Parse_SingleQuotedAttribute_ParsesCorrectly()
    {
        var html = "<div id='test-id'>Content</div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("test-id", div.Attributes["id"]);
    }

    [Fact]
    public void Parse_UnquotedAttribute_ParsesCorrectly()
    {
        var html = "<div id=test-id>Content</div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("test-id", div.Attributes["id"]);
    }

    [Fact]
    public void Parse_BooleanAttribute_ParsesAsEmptyString()
    {
        var html = "<input type=\"checkbox\" checked disabled>";
        var result = HtmlParserInstance.Parse(html);

        var input = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal(string.Empty, input.Attributes["checked"]);
        Assert.Equal(string.Empty, input.Attributes["disabled"]);
    }

    [Fact]
    public void Parse_AttributeWithSpecialCharacters_ParsesCorrectly()
    {
        var html = "<div data-value=\"test &amp; value\">Content</div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("test &amp; value", div.Attributes["data-value"]);
    }

    [Fact]
    public void Parse_MultipleAttributes_ParsesAll()
    {
        var html = "<a href=\"#\" id=\"link\" class=\"btn\" title=\"Click me\">Link</a>";
        var result = HtmlParserInstance.Parse(html);

        var a = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal(4, a.Attributes.Count);
        Assert.Equal("#", a.Attributes["href"]);
        Assert.Equal("link", a.Attributes["id"]);
        Assert.Equal("btn", a.Attributes["class"]);
        Assert.Equal("Click me", a.Attributes["title"]);
    }

    #endregion

    #region Self-Closing Tags

    [Fact]
    public void Parse_SelfClosingTag_ParsesCorrectly()
    {
        var html = "<br />";
        var result = HtmlParserInstance.Parse(html);

        var br = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.True(br.IsSelfClosing);
        Assert.True(br.IsVoidElement);
    }

    [Fact]
    public void Parse_SelfClosingTagNoSpace_ParsesCorrectly()
    {
        var html = "<br/>";
        var result = HtmlParserInstance.Parse(html);

        var br = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.True(br.IsSelfClosing);
    }

    [Fact]
    public void Parse_SelfClosingTagWithAttributes_ParsesCorrectly()
    {
        var html = "<img src=\"test.jpg\" alt=\"Test\" />";
        var result = HtmlParserInstance.Parse(html);

        var img = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.True(img.IsSelfClosing);
        Assert.Equal("test.jpg", img.Attributes["src"]);
    }

    #endregion
}
