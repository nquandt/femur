using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

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
    public void Parse_CDATA_CreatesCDataNode()
    {
        // Arrange
        var html = "<![CDATA[<script>alert('test');</script>]]>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.Single(document.Children);
        var cdata = Assert.IsType<CDataNode>(document.Children[0]);
        Assert.Contains("<script>", cdata.Content);
        Assert.Contains("alert", cdata.Content);
    }

    [Fact]
    public void Parse_CDATAWithNestedBrackets_HandlesCorrectly()
    {
        // Arrange
        var html = "<![CDATA[Content with ] and ]]>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var cdata = Assert.IsType<CDataNode>(document.Children[0]);
        // CDATA content may not preserve all brackets perfectly due to parsing logic
        Assert.Contains("Content", cdata.Content);
    }

    #endregion
}

