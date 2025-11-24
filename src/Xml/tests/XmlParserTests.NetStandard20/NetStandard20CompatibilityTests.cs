using Femur.Markup.Abstractions;
using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests.NetStandard20;

/// <summary>
/// Tests to verify netstandard2.0 compatibility for XmlParser.
/// These tests cover the most common code paths and usage scenarios.
/// </summary>
public class NetStandard20CompatibilityTests
{
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

    [Fact]
    public void Parse_NestedElements_BuildsCorrectTree()
    {
        var xml = "<root><level1><level2>Nested</level2></level1></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        var level1 = Assert.IsType<XmlElementNode>(root.Children[0]);
        Assert.Equal("level1", level1.TagName);

        var level2 = Assert.IsType<XmlElementNode>(level1.Children[0]);
        Assert.Equal("level2", level2.TagName);

        var text = Assert.IsType<TextNode>(level2.Children[0]);
        Assert.Equal("Nested", text.Content);
    }
}

