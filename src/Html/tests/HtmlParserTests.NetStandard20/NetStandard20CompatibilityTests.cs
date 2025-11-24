using Femur.Markup.Abstractions;
using Femur.Markup.Abstractions.Nodes;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests.NetStandard20;

/// <summary>
/// Tests to verify netstandard2.0 compatibility for HtmlParser.
/// These tests cover the most common code paths and usage scenarios.
/// </summary>
public class NetStandard20CompatibilityTests
{
    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptyDocument()
    {
        var result = HtmlParserInstance.Parse("");

        Assert.NotNull(result);
        Assert.Equal(MarkupNodeType.Document, result.NodeType);
        Assert.Empty(result.Children);
    }

    [Fact]
    public void Parse_BasicHtmlDocument_ReturnsCorrectStructure()
    {
        var html = "<html><head><title>Test</title></head><body><p>Content</p></body></html>";
        var result = HtmlParserInstance.Parse(html);

        Assert.NotNull(result);
        _ = Assert.Single(result.Children);

        var htmlElement = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("html", htmlElement.TagName);
        Assert.Equal(2, htmlElement.Children.Count);

        var head = Assert.IsType<ElementNode>(htmlElement.Children[0]);
        Assert.Equal("head", head.TagName);

        var body = Assert.IsType<ElementNode>(htmlElement.Children[1]);
        Assert.Equal("body", body.TagName);
    }

    [Fact]
    public void Parse_DocumentWithTitle_ParsesTitle()
    {
        var html = "<head><title>My Page Title</title></head>";
        var result = HtmlParserInstance.Parse(html);

        var head = Assert.IsType<ElementNode>(result.Children[0]);
        var title = Assert.IsType<ElementNode>(head.Children[0]);
        Assert.Equal("title", title.TagName);

        var titleText = Assert.IsType<TextNode>(title.Children[0]);
        Assert.Equal("My Page Title", titleText.Content);
    }

    [Fact]
    public void Parse_HeadingWithAttributes_ParsesAttributes()
    {
        var html = "<h1 id=\"main-title\" class=\"header\">Title</h1>";
        var result = HtmlParserInstance.Parse(html);

        var heading = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("h1", heading.TagName, ignoreCase: true);
        Assert.Equal("main-title", heading.Attributes["id"]);
        Assert.Equal("header", heading.Attributes["class"]);
    }

    [Fact]
    public void Parse_TextContent_CreatesTextNode()
    {
        var html = "<p>Hello World</p>";
        var result = HtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(result.Children[0]);
        _ = Assert.Single(p.Children);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.Equal("Hello World", text.Content);
    }
}

