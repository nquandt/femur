using Femur.Markup.Abstractions.Nodes;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

public class ParserBehaviorTests : IClassFixture<TestFixture>, IDisposable
{
    public ParserBehaviorTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }
    #region Case Insensitivity

    [Fact]
    public void Parse_UppercaseTags_ParsesCorrectly()
    {
        var html = "<DIV>Content</DIV>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("DIV", div.TagName);
    }

    [Fact]
    public void Parse_MixedCaseTags_ParsesCorrectly()
    {
        var html = "<DiV>Content</dIv>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("DiV", div.TagName);
    }

    #endregion

    #region Location Tracking

    [Fact]
    public void Parse_ElementHasLocation()
    {
        var html = "<div>Content</div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.True(div.Location.Offset >= 0);
        Assert.True(div.Location.Length > 0);
    }

    [Fact]
    public void Parse_TextNodeHasLocation()
    {
        var html = "<p>Content</p>";
        var result = HtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.True(text.Location.Offset >= 0);
        Assert.True(text.Location.Length > 0);
    }

    #endregion

    #region Static Parse Methods

    [Fact]
    public void Parse_StringOverload_ParsesCorrectly()
    {
        var html = "<div>Test</div>";
        var result = HtmlParserInstance.Parse(html);

        Assert.NotNull(result);
        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("div", div.TagName, ignoreCase: true);
    }

    [Fact]
    public void Parse_ByteArrayOverload_ParsesCorrectly()
    {
        var html = "<div>Test</div>";
        var bytes = System.Text.Encoding.UTF8.GetBytes(html);
        var result = HtmlParserInstance.Parse(bytes);

        Assert.NotNull(result);
        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("div", div.TagName, ignoreCase: true);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_EmptyTag_ParsesCorrectly()
    {
        var html = "<div></div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Empty(div.Children);
    }

    [Fact]
    public void Parse_TagWithOnlyWhitespace_FiltersWhitespace()
    {
        var html = "<div>   \n\t   </div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        // Whitespace-only text nodes should be filtered
        var textNodes = div.Children.OfType<TextNode>().ToList();
        Assert.Empty(textNodes);
    }

    [Fact]
    public void Parse_TagWithAttributesAndWhitespace_ParsesCorrectly()
    {
        var html = "<div   id=\"test\"   class=\"demo\"   >Content</div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("test", div.Attributes["id"]);
        Assert.Equal("demo", div.Attributes["class"]);
    }

    [Fact]
    public void Parse_AttributeValueWithEquals_ParsesCorrectly()
    {
        var html = "<div data-value=\"x=5\">Content</div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("x=5", div.Attributes["data-value"]);
    }

    #endregion
}
