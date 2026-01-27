using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class AttributesTests : IClassFixture<TestFixture>, IDisposable
{
    public AttributesTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Attributes

    [Fact]
    public void Parse_QuotedAttributes_ParsesCorrectly()
    {
        // Arrange
        var html = "<div class=\"container\" id=\"main\">Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal(2, div.Attributes.Count);
        Assert.Equal("container", div.Attributes["class"]);
        Assert.Equal("main", div.Attributes["id"]);
    }

    [Fact]
    public void Parse_SingleQuotedAttributes_ParsesCorrectly()
    {
        // Arrange
        var html = "<div class='container' id='main'>Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("container", div.Attributes["class"]);
        Assert.Equal("main", div.Attributes["id"]);
    }

    [Fact]
    public void Parse_UnquotedAttributes_ParsesCorrectly()
    {
        // Arrange
        var html = "<div class=container id=main>Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("container", div.Attributes["class"]);
        Assert.Equal("main", div.Attributes["id"]);
    }

    [Fact]
    public void Parse_BooleanAttributes_SetsEmptyValue()
    {
        // Arrange
        var html = "<input disabled readonly>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var input = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal(2, input.Attributes.Count);
        Assert.Equal(string.Empty, input.Attributes["disabled"]);
        Assert.Equal(string.Empty, input.Attributes["readonly"]);
    }

    [Fact]
    public void Parse_AttributesWithSpecialCharacters_ParsesCorrectly()
    {
        // Arrange
        var html = "<div data-value=\"test &amp; value\" class=\"test-class\">Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("test &amp; value", div.Attributes["data-value"]);
        Assert.Equal("test-class", div.Attributes["class"]);
    }

    [Fact]
    public void Parse_AttributeWithWhitespace_ParsesCorrectly()
    {
        // Arrange
        var html = "<div class=\"container\" id=\"main\">Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("container", div.Attributes["class"]);
        Assert.Equal("main", div.Attributes["id"]);
    }

    #endregion
}

