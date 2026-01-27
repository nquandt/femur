using System.Text;
using Femur.Chtml.Parser;
using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class EdgeCasesTests : IClassFixture<TestFixture>, IDisposable
{
    public EdgeCasesTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Edge Cases and Negative Tests

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyDocument()
    {
        // Arrange
        var html = string.Empty;

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.NotNull(document);
        Assert.Empty(document.Children);
    }

    [Fact]
    public void Parse_OnlyWhitespace_ReturnsEmptyDocument()
    {
        // Arrange
        var html = "   \n\t  ";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.Empty(document.Children);
    }

    [Fact]
    public void Parse_UnclosedTag_HandlesGracefully()
    {
        // Arrange
        var html = "<div><p>Content";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Single(div.Children);
        var p = Assert.IsType<ElementNode>(div.Children[0]);
        Assert.Equal("p", p.TagName);
    }

    [Fact]
    public void Parse_MismatchedClosingTags_HandlesGracefully()
    {
        // Arrange
        var html = "<div><p>Content</div></p>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        // Should handle mismatched tags gracefully
        Assert.Single(document.Children);
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("div", div.TagName);
    }

    [Fact]
    public void Parse_TagWithoutName_HandlesGracefully()
    {
        // Arrange
        var html = "<>Content</>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        // Should handle gracefully - might create empty tag or treat as text
        Assert.NotNull(document);
    }

    [Fact]
    public void Parse_AttributesWithoutValues_HandlesCorrectly()
    {
        // Arrange
        var html = "<input disabled checked>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var input = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal(2, input.Attributes.Count);
        Assert.Equal(string.Empty, input.Attributes["disabled"]);
        Assert.Equal(string.Empty, input.Attributes["checked"]);
    }

    [Fact]
    public void Parse_StreamFromBytes_ParsesCorrectly()
    {
        // Arrange
        var html = "<div>Test</div>";
        var bytes = Encoding.UTF8.GetBytes(html);
        using var stream = new MemoryStream(bytes);

        // Act
        var parser = new ChtmlParserInstance(stream);
        var document = parser.Parse();

        // Assert
        Assert.NotNull(document);
        Assert.Single(document.Children);
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("div", div.TagName);
    }

    [Fact]
    public void Parse_LargeDocument_ParsesCorrectly()
    {
        // Arrange
        var html = new StringBuilder();
        html.Append("<div>");
        for (int i = 0; i < 100; i++)
        {
            html.Append($"<p>Paragraph {i}</p>");
        }
        html.Append("</div>");

        // Act
        var document = ChtmlParserInstance.Parse(html.ToString());

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        // Find all paragraph elements (may have text nodes between them)
        var paragraphs = div.Children.OfType<ElementNode>().ToList();
        Assert.Equal(100, paragraphs.Count);
    }

    [Fact]
    public void Parse_ComponentWithoutClosingTag_HandlesGracefully()
    {
        // Arrange
        var html = "<div><:Layout>Content";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Single(div.Children);
        var component = Assert.IsType<ComponentNode>(div.Children[0]);
        Assert.Equal("Layout", component.ComponentName);
    }

    [Fact]
    public void Parse_AttributesWithEqualsSignInValue_ParsesCorrectly()
    {
        // Arrange
        var html = "<div data-value=\"test=value\">Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal("test=value", div.Attributes["data-value"]);
    }

    [Fact]
    public void Parse_CommentInAttributeValue_HandlesCorrectly()
    {
        // Arrange
        var html = "<div title=\"<!-- comment -->\">Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Contains("<!-- comment -->", div.Attributes["title"]);
    }

    [Fact]
    public void Parse_CodeBlockInAttribute_HandlesCorrectly()
    {
        // Arrange
        var html = "<div class=\"test {dynamic}\">Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        // Code blocks in attributes should be treated as regular text
        Assert.Contains("test", div.Attributes["class"]);
    }

    [Fact]
    public void Parse_MixedContent_ParsesCorrectly()
    {
        // Arrange
        var html = "<div><!-- Comment --><p>Text {code}</p><:Component />More Text</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        // Comment, p, Component, and Text (whitespace may be filtered)
        Assert.True(div.Children.Count >= 3);
        Assert.Contains(div.Children, c => c is CommentNode);
        Assert.Contains(div.Children, c => c is ElementNode);
        Assert.Contains(div.Children, c => c is ComponentNode);
    }

    #endregion
}

