using Femur.Markup.Abstractions;
using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests.NetStandard20;

/// <summary>
/// Tests to verify netstandard2.0 compatibility for ChtmlParser.
/// These tests cover the most common code paths and usage scenarios.
/// </summary>
public class NetStandard20CompatibilityTests
{
    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptyDocument()
    {
        var html = "";
        var document = ChtmlParserInstance.Parse(html);

        Assert.NotNull(document);
        Assert.Equal(MarkupNodeType.Document, document.NodeType);
        Assert.Empty(document.Children);
    }

    [Fact]
    public void Parse_SimpleHtml_ReturnsDocumentNode()
    {
        var html = "<html><head><title>Test</title></head><body>Body</body></html>";
        var document = ChtmlParserInstance.Parse(html);

        Assert.NotNull(document);
        Assert.Equal(MarkupNodeType.Document, document.NodeType);
        Assert.Single(document.Children);
        Assert.IsType<ElementNode>(document.Children[0]);
    }

    [Fact]
    public void Parse_NestedElements_BuildsCorrectTree()
    {
        var html = "<div><p>Hello</p><p>World</p></div>";
        var document = ChtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("div", div.TagName);

        var paragraphs = div.Children.OfType<ElementNode>().ToList();
        Assert.Equal(2, paragraphs.Count);

        Assert.Equal("p", paragraphs[0].TagName);
        var text1 = Assert.IsType<TextNode>(paragraphs[0].Children[0]);
        Assert.Equal("Hello", text1.Content);

        Assert.Equal("p", paragraphs[1].TagName);
        var text2 = Assert.IsType<TextNode>(paragraphs[1].Children[0]);
        Assert.Equal("World", text2.Content);
    }

    [Fact]
    public void Parse_TextContent_CreatesTextNode()
    {
        var html = "<p>Hello World</p>";
        var document = ChtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Single(p.Children);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.Equal("Hello World", text.Content);
    }

    [Fact]
    public void Parse_EmptyElement_ParsesCorrectly()
    {
        var html = "<div></div>";
        var document = ChtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("div", div.TagName);
        Assert.Empty(div.Children);
    }
}

