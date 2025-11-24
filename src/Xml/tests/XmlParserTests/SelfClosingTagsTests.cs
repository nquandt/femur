using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

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
    public void Parse_SelfClosingTag_ParsesCorrectly()
    {
        var xml = "<root />";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.True(root.IsSelfClosing);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Parse_SelfClosingTagNoSpace_ParsesCorrectly()
    {
        var xml = "<root/>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.True(root.IsSelfClosing);
    }

    [Fact]
    public void Parse_SelfClosingTagWithAttributes_ParsesCorrectly()
    {
        var xml = "<root id=\"test\" />";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.True(root.IsSelfClosing);
        Assert.Equal("test", root.Attributes["id"]);
    }

    #endregion
}

