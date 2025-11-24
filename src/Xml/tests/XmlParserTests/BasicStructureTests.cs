using Femur.Markup.Abstractions;
using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class BasicStructureTests : IClassFixture<TestFixture>, IDisposable
{
    public BasicStructureTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Basic Document Structure

    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptyDocument()
    {
        var result = XmlParserInstance.Parse("");

        Assert.NotNull(result);
        Assert.Equal(MarkupNodeType.Document, result.NodeType);
        Assert.Empty(result.Children);
    }

    [Fact]
    public void Parse_SimpleXmlDocument_ReturnsCorrectStructure()
    {
        var xml = "<root><child>Content</child></root>";
        var result = XmlParserInstance.Parse(xml);

        Assert.NotNull(result);
        _ = Assert.Single(result.Children);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("root", root.TagName);
        _ = Assert.Single(root.Children);

        var child = Assert.IsType<XmlElementNode>(root.Children[0]);
        Assert.Equal("child", child.TagName);
    }

    [Fact]
    public void Parse_DocumentWithTextContent_ParsesText()
    {
        var xml = "<root>Text content</root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(root.Children[0]);
        Assert.Equal("Text content", text.Content);
    }

    #endregion
}

