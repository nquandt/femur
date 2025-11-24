using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

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
    public void Parse_ElementHasLocation()
    {
        var xml = "<root>Content</root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.True(root.Location.Offset >= 0);
        Assert.True(root.Location.Length > 0);
    }

    [Fact]
    public void Parse_TextNodeHasLocation()
    {
        var xml = "<root>Content</root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(root.Children[0]);
        Assert.True(text.Location.Offset >= 0);
        Assert.True(text.Location.Length > 0);
    }

    [Fact]
    public void Parse_CommentHasLocation()
    {
        var xml = "<!-- comment --><root></root>";
        var result = XmlParserInstance.Parse(xml);

        var comment = Assert.IsType<CommentNode>(result.Children[0]);
        Assert.True(comment.Location.Offset >= 0);
        Assert.True(comment.Location.Length > 0);
    }

    #endregion
}

