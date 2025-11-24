using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class CDataTests : IClassFixture<TestFixture>, IDisposable
{
    public CDataTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region CDATA

    [Fact]
    public void Parse_CData_ParsesAsCDataNode()
    {
        var xml = "<![CDATA[<div>Raw content</div>]]><root></root>";
        var result = XmlParserInstance.Parse(xml);

        Assert.Equal(2, result.Children.Count);
        var cdata = Assert.IsType<CDataNode>(result.Children[0]);
        Assert.Contains("<div>Raw content</div>", cdata.Content);
    }

    [Fact]
    public void Parse_CDataWithBrackets_ParsesCorrectly()
    {
        var xml = "<![CDATA[Content with ] brackets]]><root></root>";
        var result = XmlParserInstance.Parse(xml);

        var cdata = Assert.IsType<CDataNode>(result.Children[0]);
        Assert.Contains("Content", cdata.Content);
        Assert.Contains("brackets", cdata.Content);
    }

    [Fact]
    public void Parse_CDataInElement_ParsesCorrectly()
    {
        var xml = "<root><![CDATA[CDATA content]]></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        var cdata = Assert.IsType<CDataNode>(root.Children[0]);
        Assert.Contains("CDATA content", cdata.Content);
    }

    #endregion
}

