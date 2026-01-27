using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class VoidElementsTests : IClassFixture<TestFixture>, IDisposable
{
    public VoidElementsTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Void Elements

    [Fact]
    public void Parse_VoidElement_SetsIsVoidElement()
    {
        // Arrange
        var html = "<img src=\"test.jpg\">";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var img = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("img", img.TagName);
        Assert.True(img.IsVoidElement);
        Assert.Empty(img.Children);
    }

    [Fact]
    public void Parse_VoidElementWithChildren_IgnoresChildren()
    {
        // Arrange
        var html = "<br>This should be ignored</br>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var br = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("br", br.TagName);
        Assert.True(br.IsVoidElement);
        // Void elements don't have children even if closing tag exists
        Assert.Empty(br.Children);
    }

    #endregion
}

