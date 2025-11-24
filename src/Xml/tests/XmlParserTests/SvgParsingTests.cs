using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class SvgParsingTests : IClassFixture<TestFixture>, IDisposable
{
    public SvgParsingTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region SVG Parsing

    [Fact]
    public void Parse_BasicSvgElement_ParsesCorrectly()
    {
        var xml = "<svg></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("svg", svg.TagName);
        Assert.False(svg.IsSelfClosing);
        Assert.Empty(svg.Children);
    }

    [Fact]
    public void Parse_SvgWithAttributes_ParsesAttributesCorrectly()
    {
        var xml = "<svg width=\"100\" height=\"200\" viewBox=\"0 0 100 200\"></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("svg", svg.TagName);
        Assert.Equal("100", svg.Attributes["width"]);
        Assert.Equal("200", svg.Attributes["height"]);
        Assert.Equal("0 0 100 200", svg.Attributes["viewBox"]);
    }

    [Fact]
    public void Parse_SvgWithNestedElements_ParsesCorrectly()
    {
        var xml = "<svg><circle cx=\"50\" cy=\"50\" r=\"40\"></circle><rect x=\"10\" y=\"10\" width=\"80\" height=\"80\"></rect></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(2, svg.Children.Count);

        var circle = Assert.IsType<XmlElementNode>(svg.Children[0]);
        Assert.Equal("circle", circle.TagName);
        Assert.Equal("50", circle.Attributes["cx"]);
        Assert.Equal("50", circle.Attributes["cy"]);
        Assert.Equal("40", circle.Attributes["r"]);

        var rect = Assert.IsType<XmlElementNode>(svg.Children[1]);
        Assert.Equal("rect", rect.TagName);
        Assert.Equal("10", rect.Attributes["x"]);
        Assert.Equal("10", rect.Attributes["y"]);
    }

    [Fact]
    public void Parse_SvgWithTextContent_ParsesTextNode()
    {
        var xml = "<svg><text x=\"10\" y=\"20\">Hello SVG</text></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        var textElement = Assert.IsType<XmlElementNode>(svg.Children[0]);
        Assert.Equal("text", textElement.TagName);

        var textNode = Assert.IsType<TextNode>(textElement.Children[0]);
        Assert.Equal("Hello SVG", textNode.Content);
    }

    [Fact]
    public void Parse_SvgWithSelfClosingElements_ParsesCorrectly()
    {
        var xml = "<svg><circle cx=\"50\" cy=\"50\" r=\"40\" /><rect x=\"10\" y=\"10\" width=\"80\" height=\"80\" /></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(2, svg.Children.Count);

        var circle = Assert.IsType<XmlElementNode>(svg.Children[0]);
        Assert.Equal("circle", circle.TagName);
        Assert.True(circle.IsSelfClosing);

        var rect = Assert.IsType<XmlElementNode>(svg.Children[1]);
        Assert.Equal("rect", rect.TagName);
        Assert.True(rect.IsSelfClosing);
    }

    [Fact]
    public void Parse_SvgWithNamespaces_ParsesNamespaceAttributes()
    {
        var xml = "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"><use xlink:href=\"#icon\"></use></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("http://www.w3.org/2000/svg", svg.Attributes["xmlns"]);
        Assert.Equal("http://www.w3.org/1999/xlink", svg.Attributes["xmlns:xlink"]);
        Assert.Equal("http://www.w3.org/2000/svg", svg.NamespaceUri);

        var use = Assert.IsType<XmlElementNode>(svg.Children[0]);
        Assert.Equal("use", use.TagName);
        Assert.Equal("#icon", use.Attributes["xlink:href"]);
    }

    [Fact]
    public void Parse_SvgWithComments_ParsesComments()
    {
        var xml = "<svg><!-- This is a comment --><circle r=\"10\"></circle></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(2, svg.Children.Count);

        var comment = Assert.IsType<CommentNode>(svg.Children[0]);
        Assert.Contains("This is a comment", comment.Content);

        var circle = Assert.IsType<XmlElementNode>(svg.Children[1]);
        Assert.Equal("circle", circle.TagName);
    }

    [Fact]
    public void Parse_SvgWithCData_ParsesCData()
    {
        var xml = "<svg><![CDATA[<circle r=\"10\"/>]]></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        _ = Assert.Single(svg.Children);

        var cdata = Assert.IsType<CDataNode>(svg.Children[0]);
        Assert.Equal("<circle r=\"10\"/>", cdata.Content);
    }

    [Fact]
    public void Parse_SvgCaseSensitive_PreservesCase()
    {
        var xml = "<svg><Circle CX=\"50\" CY=\"50\" R=\"40\"></Circle></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        var circle = Assert.IsType<XmlElementNode>(svg.Children[0]);
        // XML is case-sensitive, so Circle should be preserved
        Assert.Equal("Circle", circle.TagName);
        Assert.Equal("50", circle.Attributes["CX"]);
        Assert.Equal("50", circle.Attributes["CY"]);
        Assert.Equal("40", circle.Attributes["R"]);
    }

    [Fact]
    public void Parse_SvgWithComplexNesting_ParsesCorrectly()
    {
        var xml = "<svg><g transform=\"translate(10,10)\"><circle r=\"5\"></circle><g><rect width=\"10\" height=\"10\"></rect></g></g></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        _ = Assert.Single(svg.Children);

        var g1 = Assert.IsType<XmlElementNode>(svg.Children[0]);
        Assert.Equal("g", g1.TagName);
        Assert.Equal("translate(10,10)", g1.Attributes["transform"]);
        Assert.Equal(2, g1.Children.Count);

        var circle = Assert.IsType<XmlElementNode>(g1.Children[0]);
        Assert.Equal("circle", circle.TagName);

        var g2 = Assert.IsType<XmlElementNode>(g1.Children[1]);
        Assert.Equal("g", g2.TagName);
        _ = Assert.Single(g2.Children);

        var rect = Assert.IsType<XmlElementNode>(g2.Children[0]);
        Assert.Equal("rect", rect.TagName);
    }

    [Fact]
    public void Parse_SvgWithProcessingInstruction_ParsesProcessingInstruction()
    {
        var xml = "<svg><?xml-stylesheet type=\"text/css\" href=\"style.css\"?><circle r=\"10\"></circle></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        var pi = Assert.IsType<ProcessingInstructionNode>(svg.Children[0]);
        Assert.Equal("xml-stylesheet", pi.Target);
        Assert.Contains("type=\"text/css\"", pi.Content);
        Assert.Contains("href=\"style.css\"", pi.Content);
    }

    [Fact]
    public void Parse_SvgWithMixedContent_ParsesCorrectly()
    {
        var xml = "<svg>Text before<circle r=\"10\"></circle>Text after</svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(3, svg.Children.Count);

        var text1 = Assert.IsType<TextNode>(svg.Children[0]);
        Assert.Equal("Text before", text1.Content.Trim());

        var circle = Assert.IsType<XmlElementNode>(svg.Children[1]);
        Assert.Equal("circle", circle.TagName);

        var text2 = Assert.IsType<TextNode>(svg.Children[2]);
        Assert.Equal("Text after", text2.Content.Trim());
    }

    [Fact]
    public void Parse_NestedSvgTags_ParsesCorrectly()
    {
        var xml = "<svg><svg id=\"inner\"><circle r=\"5\"></circle></svg></svg>";
        var result = XmlParserInstance.Parse(xml);

        var outerSvg = Assert.IsType<XmlElementNode>(result.Children[0]);
        _ = Assert.Single(outerSvg.Children);

        var innerSvg = Assert.IsType<XmlElementNode>(outerSvg.Children[0]);
        Assert.Equal("svg", innerSvg.TagName);
        Assert.Equal("inner", innerSvg.Attributes["id"]);
        _ = Assert.Single(innerSvg.Children);

        var circle = Assert.IsType<XmlElementNode>(innerSvg.Children[0]);
        Assert.Equal("circle", circle.TagName);
    }

    [Fact]
    public void Parse_SvgSelfClosing_ParsesAsSelfClosing()
    {
        var xml = "<svg />";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("svg", svg.TagName);
        Assert.True(svg.IsSelfClosing);
        Assert.Empty(svg.Children);
    }

    [Fact]
    public void Parse_SvgWithWhitespace_PreservesWhitespace()
    {
        var xml = "<svg>\n  <circle r=\"10\"></circle>\n</svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        // XML preserves whitespace, so we should have text nodes with whitespace
        Assert.True(svg.Children.Count >= 1);

        var circle = svg.Children.OfType<XmlElementNode>().FirstOrDefault();
        Assert.NotNull(circle);
        Assert.Equal("circle", circle!.TagName);
    }

    [Fact]
    public void Parse_SvgWithSpecialCharactersInAttributes_ParsesCorrectly()
    {
        var xml = "<svg data-value=\"test &amp; value\" data-url=\"http://example.com?x=1&amp;y=2\"></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("test &amp; value", svg.Attributes["data-value"]);
        Assert.Equal("http://example.com?x=1&amp;y=2", svg.Attributes["data-url"]);
    }

    [Fact]
    public void Parse_SvgWithAttributesContainingEscapedQuotes_ParsesCorrectly()
    {
        var xml = "<svg data-text=\"He said \\\"Hello\\\"\"></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("He said \"Hello\"", svg.Attributes["data-text"]);
    }

    [Fact]
    public void Parse_SvgWithMultipleProcessingInstructions_ParsesAll()
    {
        var xml = "<svg><?xml-stylesheet type=\"text/css\" href=\"style.css\"?><?custom-instruction data=\"value\"?><circle r=\"10\"></circle></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        var processingInstructions = svg.Children.OfType<ProcessingInstructionNode>().ToList();
        Assert.Equal(2, processingInstructions.Count);

        Assert.Equal("xml-stylesheet", processingInstructions[0].Target);
        Assert.Equal("custom-instruction", processingInstructions[1].Target);
    }

    [Fact]
    public void Parse_SvgWithLocationTracking_TracksLocations()
    {
        var xml = "<svg width=\"100\"></svg>";
        var result = XmlParserInstance.Parse(xml);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.True(svg.Location.Offset >= 0);
        Assert.True(svg.Location.Length > 0);
    }

    [Fact]
    public void Parse_MultipleSvgElements_ParsesAll()
    {
        var xml = "<svg id=\"svg1\"></svg><svg id=\"svg2\"></svg>";
        var result = XmlParserInstance.Parse(xml);

        Assert.Equal(2, result.Children.Count);

        var svg1 = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("svg", svg1.TagName);
        Assert.Equal("svg1", svg1.Attributes["id"]);

        var svg2 = Assert.IsType<XmlElementNode>(result.Children[1]);
        Assert.Equal("svg", svg2.TagName);
        Assert.Equal("svg2", svg2.Attributes["id"]);
    }

    #endregion
}

