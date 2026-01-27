using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class CommentsTests : IClassFixture<TestFixture>, IDisposable
{
    public CommentsTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Comments

    [Fact]
    public void Parse_Comment_CreatesCommentNode()
    {
        // Arrange
        var html = "<!-- This is a comment -->";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.Single(document.Children);
        var comment = Assert.IsType<CommentNode>(document.Children[0]);
        // Parser includes the dash and space after <!--
        Assert.Contains("This is a comment", comment.Content);
    }

    [Fact]
    public void Parse_CommentWithDashes_HandlesCorrectly()
    {
        // Arrange
        var html = "<!--- Multi-dash comment --->";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var comment = Assert.IsType<CommentNode>(document.Children[0]);
        Assert.Contains("Multi-dash", comment.Content);
    }

    [Fact]
    public void Parse_CommentInElement_ParsesCorrectly()
    {
        // Arrange
        var html = "<div><!-- Comment -->Content</div>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var div = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal(2, div.Children.Count);
        Assert.IsType<CommentNode>(div.Children[0]);
        var text = Assert.IsType<TextNode>(div.Children[1]);
        Assert.Equal("Content", text.Content);
    }

    #endregion
}

