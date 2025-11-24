using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

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

    #region Edge Cases

    [Fact]
    public void Parse_EmptyElement_ParsesCorrectly()
    {
        var xml = "<root></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Parse_ElementWithOnlyWhitespace_FiltersWhitespace()
    {
        var xml = "<root>   \n\t   </root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Parse_ElementWithAttributesAndWhitespace_ParsesCorrectly()
    {
        var xml = "<root   id=\"test\"   class=\"demo\"   >Content</root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("test", root.Attributes["id"]);
        Assert.Equal("demo", root.Attributes["class"]);
    }

    [Fact]
    public void Parse_MultipleRootElements_ParsesAll()
    {
        var xml = "<root1></root1><root2></root2>";
        var result = XmlParserInstance.Parse(xml);

        Assert.Equal(2, result.Children.Count);
        Assert.All(result.Children, item => Assert.IsType<XmlElementNode>(item));
    }

    #endregion
}

