using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class NamespacesTests : IClassFixture<TestFixture>, IDisposable
{
    public NamespacesTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Namespaces

    [Fact]
    public void Parse_ElementWithNamespacePrefix_ParsesPrefix()
    {
        var xml = "<ns:root></ns:root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("ns:root", root.TagName);
        Assert.Equal("ns", root.NamespacePrefix);
        Assert.Equal("root", root.LocalName);
    }

    [Fact]
    public void Parse_ElementWithXmlnsAttribute_ParsesNamespace()
    {
        var xml = "<root xmlns=\"http://example.com\"></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("http://example.com", root.NamespaceUri);
    }

    [Fact]
    public void Parse_ElementWithPrefixedXmlnsAttribute_ParsesNamespace()
    {
        var xml = "<root xmlns:ns=\"http://example.com\"><ns:child></ns:child></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.True(root.Attributes.ContainsKey("xmlns:ns"));
        Assert.Equal("http://example.com", root.Attributes["xmlns:ns"]);
    }

    [Fact]
    public void Parse_NestedElementsWithNamespaces_ParsesCorrectly()
    {
        var xml = "<ns1:root><ns2:child></ns2:child></ns1:root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("ns1", root.NamespacePrefix);
        var child = Assert.IsType<XmlElementNode>(root.Children[0]);
        Assert.Equal("ns2", child.NamespacePrefix);
    }

    #endregion
}

