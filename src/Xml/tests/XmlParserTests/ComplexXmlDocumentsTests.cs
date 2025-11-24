using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class ComplexXmlDocumentsTests : IClassFixture<TestFixture>, IDisposable
{
    public ComplexXmlDocumentsTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Complex XML Documents

    [Fact]
    public void Parse_ComplexXmlDocument_ParsesCorrectly()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <root xmlns="http://example.com" xmlns:ns="http://example.com/ns">
                <element id="1" class="test">Content</element>
                <ns:nested>
                    <child>Nested content</child>
                </ns:nested>
                <!-- Comment -->
                <![CDATA[<raw>CDATA</raw>]]>
                <self-closing id="test" />
            </root>
            """;

        var result = XmlParserInstance.Parse(xml);

        Assert.NotNull(result);
        Assert.NotNull(result.XmlDeclaration);

        var root = Assert.IsType<XmlElementNode>(result.Children[1]); // After XML declaration
        Assert.Equal("root", root.TagName);
        Assert.True(root.Attributes.ContainsKey("xmlns"));

        Assert.True(root.Children.Count > 0);
    }

    [Fact]
    public void Parse_XmlWithMixedContent_ParsesCorrectly()
    {
        var xml = "<root>Text1<child>Child text</child>Text2<child2 />Text3</root>";
        var result = XmlParserInstance.Parse(xml);

        var root = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(5, root.Children.Count); // Text1, child, Text2, child2, Text3
    }

    #endregion
}

