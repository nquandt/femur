using Femur.Chtml.Parser;
using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class ComponentTagsTests : IClassFixture<TestFixture>, IDisposable
{
    public ComponentTagsTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Component Tags

    [Fact]
    public void Parse_SelfClosingComponent_CreatesComponentNode()
    {
        // Arrange
        var html = "<:Header />";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.Single(document.Children);
        var component = Assert.IsType<ComponentNode>(document.Children[0]);
        Assert.Equal("Header", component.ComponentName);
        Assert.True(component.IsSelfClosing);
        Assert.Empty(component.Children);
    }

    [Fact]
    public void Parse_ComponentWithAttributes_ParsesAttributes()
    {
        // Arrange
        var html = "<:Layout title=\"Home Page\" class=\"dark\" />";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var component = Assert.IsType<ComponentNode>(document.Children[0]);
        Assert.Equal("Layout", component.ComponentName);
        Assert.Equal(2, component.Attributes.Count);
        Assert.Equal("Home Page", component.Attributes["title"]);
        Assert.Equal("dark", component.Attributes["class"]);
    }

    [Fact]
    public void Parse_ComponentWithChildren_ParsesChildren()
    {
        // Arrange
        var html = "<:Layout>Content</:Layout>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var component = Assert.IsType<ComponentNode>(document.Children[0]);
        Assert.Equal("Layout", component.ComponentName);
        Assert.False(component.IsSelfClosing);
        Assert.Single(component.Children);
        var text = Assert.IsType<TextNode>(component.Children[0]);
        Assert.Equal("Content", text.Content);
    }

    [Fact]
    public void Parse_ComponentWithNestedElements_ParsesNestedStructure()
    {
        // Arrange
        var html = "<:Layout><div><p>Content</p></div></:Layout>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var component = Assert.IsType<ComponentNode>(document.Children[0]);
        Assert.Single(component.Children);
        var div = Assert.IsType<ElementNode>(component.Children[0]);
        Assert.Equal("div", div.TagName);
        Assert.Single(div.Children);
        var p = Assert.IsType<ElementNode>(div.Children[0]);
        Assert.Equal("p", p.TagName);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.Equal("Content", text.Content);
    }

    [Fact]
    public void Parse_ComponentWithCodeBlocks_ParsesCorrectly()
    {
        // Arrange
        var html = "<:Layout>{RenderChildren()}</:Layout>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var component = Assert.IsType<ComponentNode>(document.Children[0]);
        Assert.Single(component.Children);
        var code = Assert.IsType<CodeNode>(component.Children[0]);
        Assert.Equal("RenderChildren()", code.Content);
    }

    [Fact]
    public void Parse_ComponentClosingTag_ParsesCorrectly()
    {
        // Arrange
        var html = "<div><:Header />Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal(2, div.Children.Count);
        Assert.IsType<ComponentNode>(div.Children[0]);
        var text = Assert.IsType<TextNode>(div.Children[1]);
        Assert.Equal("Content", text.Content);
    }

    #endregion
}

