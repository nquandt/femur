using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class XmlDeclarationTests : IClassFixture<TestFixture>, IDisposable
{
    public XmlDeclarationTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region XML Declaration

    [Fact]
    public void Parse_XmlDeclaration_ParsesCorrectly()
    {
        var xml = "<?xml version=\"1.0\"?><root></root>";
        var result = XmlParserInstance.Parse(xml);

        Assert.NotNull(result.XmlDeclaration);
        Assert.Equal("xml", result.XmlDeclaration!.Target);
        Assert.Contains("version", result.XmlDeclaration.Content);
        Assert.Contains("1.0", result.XmlDeclaration.Content);
    }

    [Fact]
    public void Parse_XmlDeclarationWithEncoding_ParsesCorrectly()
    {
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><root></root>";
        var result = XmlParserInstance.Parse(xml);

        Assert.NotNull(result.XmlDeclaration);
        Assert.Contains("encoding", result.XmlDeclaration!.Content);
        Assert.Contains("UTF-8", result.XmlDeclaration.Content);
    }

    [Fact]
    public void Parse_XmlDeclarationWithStandalone_ParsesCorrectly()
    {
        var xml = "<?xml version=\"1.0\" standalone=\"yes\"?><root></root>";
        var result = XmlParserInstance.Parse(xml);

        Assert.NotNull(result.XmlDeclaration);
        Assert.Contains("standalone", result.XmlDeclaration!.Content);
    }

    [Fact]
    public void Parse_DocumentWithoutXmlDeclaration_HasNullXmlDeclaration()
    {
        var xml = "<root></root>";
        var result = XmlParserInstance.Parse(xml);

        Assert.Null(result.XmlDeclaration);
    }

    #endregion
}

