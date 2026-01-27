using Femur.Chtml.Parser;
using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class ParentChildRelationshipsTests : IClassFixture<TestFixture>, IDisposable
{
    public ParentChildRelationshipsTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Parent-Child Relationships

    [Fact]
    public void Parse_ElementChildren_HaveCorrectParent()
    {
        // Arrange
        var html = "<div><p>Test</p></div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        var p = Assert.IsType<ElementNode>(div.Children[0]);
        Assert.NotNull(p.GetParent());
        var parent = Assert.IsAssignableFrom<ElementNode>(p.GetParent());
        Assert.Same(div, parent);
    }

    [Fact]
    public void Parse_TextNode_HasCorrectParent()
    {
        // Arrange
        var html = "<p>Test</p>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var p = Assert.IsType<ElementNode>(document.Children[0]);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.NotNull(text.GetParent());
        var parent = Assert.IsAssignableFrom<ElementNode>(text.GetParent());
        Assert.Same(p, parent);
    }

    [Fact]
    public void Parse_ComponentNode_HasCorrectParent()
    {
        // Arrange
        var html = "<div><:Header /></div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        var component = Assert.IsType<ComponentNode>(div.Children[0]);
        Assert.NotNull(component.GetParent());
        var parent = Assert.IsAssignableFrom<ElementNode>(component.GetParent());
        Assert.Same(div, parent);
    }

    #endregion
}

