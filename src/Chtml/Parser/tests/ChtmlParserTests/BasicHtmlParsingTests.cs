using Femur.Markup.Abstractions;
using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class BasicHtmlParsingTests : IClassFixture<TestFixture>, IDisposable
{
    public BasicHtmlParsingTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Basic HTML Parsing

    [Fact]
    public void Parse_SimpleHtml_ReturnsDocumentNode()
    {
        // Arrange
        var html = "<html><head><title>Test</title></head><body>Body</body></html>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.NotNull(document);
        Assert.Equal(MarkupNodeType.Document, document.NodeType);
        Assert.Single(document.Children);
        Assert.IsType<ElementNode>(document.Children[0]);
    }

    [Fact]
    public void Parse_NestedElements_BuildsCorrectTree()
    {
        // Arrange
        var html = "<div><p>Hello</p><p>World</p></div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("div", div.TagName);

        // Find paragraph elements (may have text nodes between them)
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
        // Arrange
        var html = "<p>Hello World</p>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var p = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Single(p.Children);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.Equal("Hello World", text.Content);
    }

    [Fact]
    public void Parse_WhitespaceBetweenTags_IgnoresWhitespaceOnlyText()
    {
        // Arrange
        var html = "<div>\n    <p>Test</p>\n</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        // Should only have the <p> element, whitespace-only text nodes are filtered
        Assert.Single(div.Children.OfType<ElementNode>());
    }

    [Fact]
    public void Parse_TextWithWhitespace_PreservesWhitespace()
    {
        // Arrange
        var html = "<p>Hello   World</p>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var p = Assert.IsType<ElementNode>(document.Children[0]);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.Equal("Hello   World", text.Content);
    }

    [Fact]
    public void Parse_MultipleRootElements_ParsesAll()
    {
        // Arrange
        var html = "<div>First</div><div>Second</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.Equal(2, document.Children.Count);
        var div1 = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("div", div1.TagName);
        var div2 = Assert.IsType<ElementNode>(document.Children[1]);
        Assert.Equal("div", div2.TagName);
    }

    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptyDocument()
    {
        // Arrange
        var html = "";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.NotNull(document);
        Assert.Equal(MarkupNodeType.Document, document.NodeType);
        Assert.Empty(document.Children);
    }

    [Fact]
    public void Parse_EmptyElement_ParsesCorrectly()
    {
        // Arrange
        var html = "<div></div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("div", div.TagName);
        Assert.Empty(div.Children);
    }

    [Fact]
    public void Parse_DeeplyNestedElements_ParsesCorrectly()
    {
        // Arrange
        var html = "<div><div><div><div>Deep</div></div></div></div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var level1 = Assert.IsType<ElementNode>(document.Children[0]);
        var level2 = Assert.IsType<ElementNode>(level1.Children[0]);
        var level3 = Assert.IsType<ElementNode>(level2.Children[0]);
        var level4 = Assert.IsType<ElementNode>(level3.Children[0]);
        var text = Assert.IsType<TextNode>(level4.Children[0]);
        Assert.Equal("Deep", text.Content);
    }

    #endregion
}

