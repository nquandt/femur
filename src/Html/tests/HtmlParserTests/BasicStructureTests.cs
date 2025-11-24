using Femur.Markup.Abstractions;
using Femur.Markup.Abstractions.Nodes;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

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

    #endregion

    #region Headings (h1-h6)

    [Fact]
    public void Parse_AllHeadingLevels_ParsesCorrectly()
    {
        var html = "<h1>Heading 1</h1><h2>Heading 2</h2><h3>Heading 3</h3><h4>Heading 4</h4><h5>Heading 5</h5><h6>Heading 6</h6>";
        var result = HtmlParserInstance.Parse(html);

        Assert.Equal(6, result.Children.Count);

        for (int i = 1; i <= 6; i++)
        {
            var heading = Assert.IsType<ElementNode>(result.Children[i - 1]);
            Assert.Equal($"h{i}", heading.TagName, ignoreCase: true);
            var text = Assert.IsType<TextNode>(heading.Children[0]);
            Assert.Equal($"Heading {i}", text.Content);
        }
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

    #endregion
}
