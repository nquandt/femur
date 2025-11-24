using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class NestedElementsTests : IClassFixture<TestFixture>, IDisposable
{
    public NestedElementsTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Nested Elements

    [Fact]
    public void Parse_DeeplyNestedElements_ParsesCorrectly()
    {
        var xml = "<level1><level2><level3><level4>Deep</level4></level3></level2></level1>";
        var result = XmlParserInstance.Parse(xml);

        var level1 = Assert.IsType<XmlElementNode>(result.Children[0]);
        var level2 = Assert.IsType<XmlElementNode>(level1.Children[0]);
        var level3 = Assert.IsType<XmlElementNode>(level2.Children[0]);
        var level4 = Assert.IsType<XmlElementNode>(level3.Children[0]);
        var text = Assert.IsType<TextNode>(level4.Children[0]);
        Assert.Equal("Deep", text.Content);
    }

    [Fact]
    public void Parse_MultipleSiblings_ParsesCorrectly()
    {
        var xml = "<root><child1></child1><child2></child2><child3></child3></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(3, root.Children.Count);
        Assert.All(root.Children, item => Assert.IsType<XmlElementNode>(item));
    }

    [Fact]
    public void Parse_MixedContent_ParsesCorrectly()
    {
        var xml = "<root>Text <child>child content</child> more text</root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(3, root.Children.Count);
        _ = Assert.IsType<TextNode>(root.Children[0]);
        _ = Assert.IsType<XmlElementNode>(root.Children[1]);
        _ = Assert.IsType<TextNode>(root.Children[2]);
    }

    #endregion
}

