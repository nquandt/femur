using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class DoctypeTests : IClassFixture<TestFixture>, IDisposable
{
    public DoctypeTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region DOCTYPE

    [Fact]
    public void Parse_DOCTYPE_CreatesDocumentTypeNode()
    {
        // Arrange
        var html = "<!DOCTYPE html>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.Single(document.Children);
        var doctype = Assert.IsType<DocumentTypeNode>(document.Children[0]);
        Assert.Contains("DOCTYPE", doctype.Content);
        Assert.Contains("html", doctype.Content);
    }

    [Fact]
    public void Parse_DOCTYPEWithAttributes_ParsesCorrectly()
    {
        // Arrange
        var html = "<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\">";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var doctype = Assert.IsType<DocumentTypeNode>(document.Children[0]);
        Assert.Contains("DOCTYPE", doctype.Content);
        Assert.Contains("PUBLIC", doctype.Content);
    }

    #endregion
}

