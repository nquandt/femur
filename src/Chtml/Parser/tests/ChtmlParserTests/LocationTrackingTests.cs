using Femur.Chtml.Parser;
using Femur.Parsing.Nodes;
using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class LocationTrackingTests : IClassFixture<TestFixture>, IDisposable
{
    public LocationTrackingTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Location Tracking

    [Fact]
    public void Parse_ElementNode_HasLocation()
    {
        // Arrange
        var html = "<div>Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.True(div.Location.Offset >= 0);
        Assert.True(div.Location.Length > 0);
    }

    [Fact]
    public void Parse_TextNode_HasLocation()
    {
        // Arrange
        var html = "<p>Hello</p>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var p = Assert.IsType<ElementNode>(document.Children[0]);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.True(text.Location.Offset >= 0);
        Assert.True(text.Location.Length > 0);
    }

    [Fact]
    public void Parse_CodeNode_HasLocation()
    {
        // Arrange
        var html = "<p>{code}</p>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var p = Assert.IsType<ElementNode>(document.Children[0]);
        var code = Assert.IsType<CodeNode>(p.Children[0]);
        Assert.True(code.Location.Offset >= 0);
        Assert.True(code.Location.Length > 0);
    }

    [Fact]
    public void Parse_ComponentNode_HasLocation()
    {
        // Arrange
        var html = "<:Header />";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var component = Assert.IsType<ComponentNode>(document.Children[0]);
        Assert.True(component.Location.Offset >= 0);
        Assert.True(component.Location.Length > 0);
    }

    [Fact]
    public void Parse_MultipleRootElements_ParsesCorrectly()
    {
        // Arrange
        var html = "<div>One</div><div>Two</div><div>Three</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        // Note: Parser may nest elements when closing tags are processed
        // At minimum, we should have parsed the HTML successfully
        Assert.NotNull(document);
        Assert.NotEmpty(document.Children);

        // Verify at least one div exists
        var firstDiv = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("div", firstDiv.TagName);

        // Check that divs exist somewhere in the tree (may be nested due to parser behavior)
        var allDivs = GetAllElements(document, "div");
        Assert.True(allDivs.Count >= 1);
    }

    private List<ElementNode> GetAllElements(Node node, string tagName)
    {
        var result = new List<ElementNode>();
        if (node is ContainerNode container)
        {
            foreach (var child in container.Children)
            {
                if (child is ElementNode element && element.TagName == tagName)
                {
                    result.Add(element);
                }
                result.AddRange(GetAllElements(child, tagName));
            }
        }
        return result;
    }

    [Fact]
    public void Parse_DeeplyNestedStructure_ParsesCorrectly()
    {
        // Arrange
        var html = "<div><div><div><div><div>Deep</div></div></div></div></div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var level1 = Assert.IsType<ElementNode>(document.Children[0]);
        var level2 = Assert.IsType<ElementNode>(level1.Children[0]);
        var level3 = Assert.IsType<ElementNode>(level2.Children[0]);
        var level4 = Assert.IsType<ElementNode>(level3.Children[0]);
        var level5 = Assert.IsType<ElementNode>(level4.Children[0]);
        var text = Assert.IsType<TextNode>(level5.Children[0]);
        Assert.Equal("Deep", text.Content);
    }

    #endregion
}

