using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class TextContentTests : IClassFixture<TestFixture>, IDisposable
{
    public TextContentTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Text Content

    [Fact]
    public void Parse_PlainText_ParsesAsTextNode()
    {
        var xml = "<root>Plain text content</root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(root.Children[0]);
        Assert.Equal("Plain text content", text.Content);
    }

    [Fact]
    public void Parse_TextWithWhitespace_PreservesWhitespace()
    {
        var xml = "<root>\n    \nContent\n    \n</root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(root.Children[0]);
        Assert.Contains("\n", text.Content);
        Assert.Contains("Content", text.Content);
    }

    [Fact]
    public void Parse_TextWithEntities_PreservesEntities()
    {
        var xml = "<root>&amp; &lt; &gt; &quot; &apos;</root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(root.Children[0]);
        Assert.Contains("&amp;", text.Content);
        Assert.Contains("&lt;", text.Content);
    }

    [Fact]
    public void Parse_WhitespaceOnlyText_FiltersWhitespace()
    {
        var xml = "<root>   \n\t   </root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        // XML parser filters whitespace-only text nodes
        Assert.Empty(root.Children);
    }

    #endregion
}

