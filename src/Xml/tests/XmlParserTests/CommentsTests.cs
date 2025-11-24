using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

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
    public void Parse_Comment_ParsesAsCommentNode()
    {
        var xml = "<!-- This is a comment --><root></root>";
        var result = XmlParserInstance.Parse(xml);

        Assert.Equal(2, result.Children.Count);
        var comment = Assert.IsType<CommentNode>(result.Children[0]);
        Assert.Contains("This is a comment", comment.Content);
    }

    [Fact]
    public void Parse_MultilineComment_ParsesCorrectly()
    {
        var xml = "<!--\nMulti-line\ncomment\n--><root></root>";
        var result = XmlParserInstance.Parse(xml);

        var comment = Assert.IsType<CommentNode>(result.Children[0]);
        Assert.Contains("\n", comment.Content);
    }

    [Fact]
    public void Parse_CommentWithDashes_ParsesCorrectly()
    {
        var xml = "<!-- Comment with -- dashes --><root></root>";
        var result = XmlParserInstance.Parse(xml);

        var comment = Assert.IsType<CommentNode>(result.Children[0]);
        Assert.Contains("--", comment.Content);
    }

    [Fact]
    public void Parse_CommentBetweenElements_ParsesCorrectly()
    {
        var xml = "<root><!-- comment --><child></child></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(2, root.Children.Count);
        _ = Assert.IsType<CommentNode>(root.Children[0]);
        _ = Assert.IsType<XmlElementNode>(root.Children[1]);
    }

    #endregion
}

