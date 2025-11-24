using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class CaseSensitivityTests : IClassFixture<TestFixture>, IDisposable
{
    public CaseSensitivityTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Case Sensitivity

    [Fact]
    public void Parse_CaseSensitiveTags_PreservesCase()
    {
        var xml = "<Root><Child>Content</Child></Root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("Root", root.TagName);
        var child = Assert.IsType<XmlElementNode>(root.Children[0]);
        Assert.Equal("Child", child.TagName);
    }

    [Fact]
    public void Parse_MixedCaseTags_PreservesCase()
    {
        var xml = "<RootElement><ChildElement>Content</ChildElement></RootElement>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("RootElement", root.TagName);
    }

    [Fact]
    public void Parse_CaseSensitiveAttributeNames_PreservesCase()
    {
        var xml = "<root ID=\"test\" Class=\"container\"></root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.True(root.Attributes.ContainsKey("ID"));
        Assert.True(root.Attributes.ContainsKey("Class"));
    }

    #endregion
}

