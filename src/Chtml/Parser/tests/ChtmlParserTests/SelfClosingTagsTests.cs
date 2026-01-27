using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class SelfClosingTagsTests : IClassFixture<TestFixture>, IDisposable
{
    public SelfClosingTagsTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Self-Closing Tags

    [Fact]
    public void Parse_SelfClosingTagWithSpace_SetsIsSelfClosing()
    {
        // Arrange
        var html = "<br />";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var br = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("br", br.TagName);
        Assert.True(br.IsSelfClosing);
        Assert.Empty(br.Children);
    }

    [Fact]
    public void Parse_SelfClosingTagWithoutSpace_SetsIsSelfClosing()
    {
        // Arrange
        var html = "<br/>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var br = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("br", br.TagName);
        Assert.True(br.IsSelfClosing);
    }

    [Fact]
    public void Parse_SelfClosingTagWithAttributes_ParsesAttributes()
    {
        // Arrange
        var html = "<img src=\"test.jpg\" alt=\"Image\" />";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var img = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("img", img.TagName);
        Assert.True(img.IsSelfClosing);
        Assert.Equal(2, img.Attributes.Count);
        Assert.Equal("test.jpg", img.Attributes["src"]);
        Assert.Equal("Image", img.Attributes["alt"]);
    }

    #endregion
}

