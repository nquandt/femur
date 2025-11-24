using System.Text;
using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class StaticParseMethodsTests : IClassFixture<TestFixture>, IDisposable
{
    public StaticParseMethodsTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Static Parse Methods

    [Fact]
    public void Parse_StringOverload_ParsesCorrectly()
    {
        var xml = "<root>Test</root>";
        var result = XmlParserInstance.Parse(xml);

        Assert.NotNull(result);
        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("root", root.TagName);
    }

    [Fact]
    public void Parse_ByteArrayOverload_ParsesCorrectly()
    {
        var xml = "<root>Test</root>";
        var bytes = Encoding.UTF8.GetBytes(xml);
        var result = XmlParserInstance.Parse(bytes);

        Assert.NotNull(result);
        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("root", root.TagName);
    }

    #endregion
}

