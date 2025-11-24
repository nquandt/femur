using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

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
    public void Parse_ElementWithDoubleQuotedAttribute_ParsesCorrectly()
    {
        var xml = "<root id=\"test-id\"></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("test-id", root.Attributes["id"]);
    }

    [Fact]
    public void Parse_ElementWithSingleQuotedAttribute_ParsesCorrectly()
    {
        var xml = "<root id='test-id'></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("test-id", root.Attributes["id"]);
    }

    [Fact]
    public void Parse_ElementWithMultipleAttributes_ParsesAll()
    {
        var xml = "<root id=\"test\" class=\"container\" data-value=\"123\"></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(3, root.Attributes.Count);
        Assert.Equal("test", root.Attributes["id"]);
        Assert.Equal("container", root.Attributes["class"]);
        Assert.Equal("123", root.Attributes["data-value"]);
    }

    [Fact]
    public void Parse_AttributeWithSpecialCharacters_ParsesCorrectly()
    {
        var xml = "<root data-value=\"test &amp; value\"></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("test &amp; value", root.Attributes["data-value"]);
    }

    [Fact]
    public void Parse_AttributeValueWithEquals_ParsesCorrectly()
    {
        var xml = "<root data-value=\"x=5\"></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("x=5", root.Attributes["data-value"]);
    }

    #endregion
}

