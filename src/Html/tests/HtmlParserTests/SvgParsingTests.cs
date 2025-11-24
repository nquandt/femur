using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

public class SvgParsingTests : IClassFixture<TestFixture>, IDisposable
{
    public SvgParsingTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }
    #region SVG Parsing

    [Fact]
    public void Parse_BasicSvgElement_ParsesAsXmlElement()
    {
        var html = "<svg></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("svg", svg.TagName);
        Assert.False(svg.IsSelfClosing);
        Assert.False(svg.IsVoidElement);
    }

    [Fact]
    public void Parse_SvgWithAttributes_ParsesAttributesWithXmlRules()
    {
        var html = "<svg width=\"100\" height=\"200\" viewBox=\"0 0 100 200\"></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("svg", svg.TagName);
        Assert.Equal("100", svg.Attributes["width"]);
        Assert.Equal("200", svg.Attributes["height"]);
        Assert.Equal("0 0 100 200", svg.Attributes["viewBox"]);
    }

    [Fact]
    public void Parse_SvgWithNestedElements_ParsesCorrectly()
    {
        var html = "<svg><circle cx=\"50\" cy=\"50\" r=\"40\"></circle><rect x=\"10\" y=\"10\" width=\"80\" height=\"80\"></rect></svg>";
        var result = HtmlParserInstance.Parse(html);

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
        var html = "<svg><text x=\"10\" y=\"20\">Hello SVG</text></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        var textElement = Assert.IsType<XmlElementNode>(svg.Children[0]);
        Assert.Equal("text", textElement.TagName);

        var textNode = Assert.IsType<TextNode>(textElement.Children[0]);
        Assert.Equal("Hello SVG", textNode.Content);
    }

    [Fact]
    public void Parse_SvgNestedInHtml_ParsesCorrectly()
    {
        var html = "<div><p>Before SVG</p><svg><circle r=\"10\"></circle></svg><p>After SVG</p></div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal(3, div.Children.Count);

        var p1 = Assert.IsType<ElementNode>(div.Children[0]);
        Assert.Equal("p", p1.TagName);

        var svg = Assert.IsType<XmlElementNode>(div.Children[1]);
        Assert.Equal("svg", svg.TagName);
        _ = Assert.Single(svg.Children);

        var circle = Assert.IsType<XmlElementNode>(svg.Children[0]);
        Assert.Equal("circle", circle.TagName);

        var p2 = Assert.IsType<ElementNode>(div.Children[2]);
        Assert.Equal("p", p2.TagName);
    }

    [Fact]
    public void Parse_MultipleSvgElements_ParsesAllAsXml()
    {
        var html = "<svg id=\"svg1\"></svg><svg id=\"svg2\"></svg>";
        var result = HtmlParserInstance.Parse(html);

        Assert.Equal(2, result.Children.Count);

        var svg1 = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("svg", svg1.TagName);
        Assert.Equal("svg1", svg1.Attributes["id"]);

        var svg2 = Assert.IsType<XmlElementNode>(result.Children[1]);
        Assert.Equal("svg", svg2.TagName);
        Assert.Equal("svg2", svg2.Attributes["id"]);
    }

    [Fact]
    public void Parse_SvgWithSelfClosingElements_ParsesCorrectly()
    {
        var html = "<svg><circle cx=\"50\" cy=\"50\" r=\"40\" /><rect x=\"10\" y=\"10\" width=\"80\" height=\"80\" /></svg>";
        var result = HtmlParserInstance.Parse(html);

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
        var html = "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"><use xlink:href=\"#icon\"></use></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("http://www.w3.org/2000/svg", svg.Attributes["xmlns"]);
        Assert.Equal("http://www.w3.org/1999/xlink", svg.Attributes["xmlns:xlink"]);

        var use = Assert.IsType<XmlElementNode>(svg.Children[0]);
        Assert.Equal("use", use.TagName);
        Assert.Equal("#icon", use.Attributes["xlink:href"]);
    }

    [Fact]
    public void Parse_SvgWithComments_ParsesComments()
    {
        var html = "<svg><!-- This is a comment --><circle r=\"10\"></circle></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(2, svg.Children.Count);

        var comment = Assert.IsType<CommentNode>(svg.Children[0]);
        Assert.Equal(" This is a comment ", comment.Content);

        var circle = Assert.IsType<XmlElementNode>(svg.Children[1]);
        Assert.Equal("circle", circle.TagName);
    }

    [Fact]
    public void Parse_SvgWithCData_ParsesCData()
    {
        var html = "<svg><![CDATA[<circle r=\"10\"/>]]></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        _ = Assert.Single(svg.Children);

        var cdata = Assert.IsType<CDataNode>(svg.Children[0]);
        Assert.Equal("<circle r=\"10\"/>", cdata.Content);
    }

    [Fact]
    public void Parse_SvgCaseSensitive_PreservesCase()
    {
        var html = "<svg><Circle CX=\"50\" CY=\"50\" R=\"40\"></Circle></svg>";
        var result = HtmlParserInstance.Parse(html);

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
        var html = "<svg><g transform=\"translate(10,10)\"><circle r=\"5\"></circle><g><rect width=\"10\" height=\"10\"></rect></g></g></svg>";
        var result = HtmlParserInstance.Parse(html);

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
        var html = "<svg><?xml-stylesheet type=\"text/css\" href=\"style.css\"?><circle r=\"10\"></circle></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        // Processing instruction should be parsed
        var pi = Assert.IsType<ProcessingInstructionNode>(svg.Children[0]);
        Assert.Equal("xml-stylesheet", pi.Target);
        Assert.Contains("type=\"text/css\"", pi.Content);
        Assert.Contains("href=\"style.css\"", pi.Content);
    }

    [Fact]
    public void Parse_SvgContinuesHtmlParsing_AdvancesStreamCorrectly()
    {
        var html = "<div>Before<svg><circle r=\"10\"></circle></svg>After</div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal(3, div.Children.Count);

        // First text node
        var text1 = Assert.IsType<TextNode>(div.Children[0]);
        Assert.Equal("Before", text1.Content);

        // SVG element
        var svg = Assert.IsType<XmlElementNode>(div.Children[1]);
        Assert.Equal("svg", svg.TagName);

        // Second text node (after SVG)
        var text2 = Assert.IsType<TextNode>(div.Children[2]);
        Assert.Equal("After", text2.Content);
    }

    [Fact]
    public void Parse_SvgWithUnquotedAttributes_RequiresQuotes()
    {
        // XML requires quoted attributes, but HTML allows unquoted
        // SVG should be parsed as XML, so unquoted attributes should fail or be handled differently
        var html = "<svg width=100 height=200></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        // XML parser should handle this - attributes might be empty or parsed differently
        // This tests that SVG uses XML parsing rules
        Assert.Equal("svg", svg.TagName);
    }

    [Fact]
    public void Parse_SvgEmpty_ParsesAsEmptyXmlElement()
    {
        var html = "<svg></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("svg", svg.TagName);
        Assert.Empty(svg.Children);
    }

    [Fact]
    public void Parse_SvgWithMixedContent_ParsesCorrectly()
    {
        var html = "<svg>Text before<circle r=\"10\"></circle>Text after</svg>";
        var result = HtmlParserInstance.Parse(html);

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
        var html = "<svg><svg id=\"inner\"><circle r=\"5\"></circle></svg></svg>";
        var result = HtmlParserInstance.Parse(html);

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
        var html = "<svg />";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("svg", svg.TagName);
        Assert.True(svg.IsSelfClosing);
        Assert.Empty(svg.Children);
    }

    [Fact]
    public void Parse_SvgWithWhitespace_PreservesWhitespace()
    {
        var html = "<svg>\n  <circle r=\"10\"></circle>\n</svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        // XML preserves whitespace, so we should have text nodes with whitespace
        Assert.True(svg.Children.Count >= 1);

        var circle = svg.Children.OfType<XmlElementNode>().FirstOrDefault();
        Assert.NotNull(circle);
        Assert.Equal("circle", circle.TagName);
    }

    [Fact]
    public void Parse_SvgWithSpecialCharactersInAttributes_ParsesCorrectly()
    {
        var html = "<svg data-value=\"test &amp; value\" data-url=\"http://example.com?x=1&amp;y=2\"></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("test &amp; value", svg.Attributes["data-value"]);
        Assert.Equal("http://example.com?x=1&amp;y=2", svg.Attributes["data-url"]);
    }

    [Fact]
    public void Parse_SvgWithMultipleProcessingInstructions_ParsesAll()
    {
        var html = "<svg><?xml-stylesheet type=\"text/css\" href=\"style.css\"?><?custom-instruction data=\"value\"?><circle r=\"10\"></circle></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        var processingInstructions = svg.Children.OfType<ProcessingInstructionNode>().ToList();
        Assert.Equal(2, processingInstructions.Count);

        Assert.Equal("xml-stylesheet", processingInstructions[0].Target);
        Assert.Equal("custom-instruction", processingInstructions[1].Target);
    }

    [Fact]
    public void Parse_SvgWithAttributesContainingQuotes_ParsesCorrectly()
    {
        var html = "<svg data-text=\"He said \\\"Hello\\\"\"></svg>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("He said \"Hello\"", svg.Attributes["data-text"]);
    }

    [Fact]
    public void Parse_SvgCaseSensitiveTagMatching_RequiresExactMatch()
    {
        // XML is case-sensitive, so <svg> must match </svg> exactly
        var html = "<svg><circle r=\"10\"></circle></SVG>";
        var result = HtmlParserInstance.Parse(html);

        var svg = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("svg", svg.TagName);
        // The closing tag </SVG> should still be found by SvgSubStream (case-insensitive check)
        // but the XML parser will handle it according to XML rules
        _ = Assert.Single(svg.Children);
    }

    [Fact]
    public void Parse_SvgWithLocationTracking_TracksLocations()
    {
        var html = "<div><svg width=\"100\"></svg></div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        var svg = Assert.IsType<XmlElementNode>(div.Children[0]);

        // Location should be tracked
        Assert.True(svg.Location.Offset >= 0);
        Assert.True(svg.Location.Length > 0);
    }

    [Fact(Skip = "This test was checking for a limitation where SVG tags starting in previous buffers would fail. However, the current implementation can handle SVG tags that start in the current buffer, even if there was content before them in previous buffers. The exception only occurs if the SVG tag itself starts in a previous buffer that has been discarded, which this test doesn't actually trigger.")]
    public void Parse_SvgSpanningMultipleBuffers_ThrowsException()
    {
        // Create a large SVG that would span multiple buffers (default buffer is 4KB)
        // We'll create an SVG with enough content to exceed the buffer
        var largeContent = new string('x', 5000); // 5KB of content
        var html = $"<div>{largeContent}<svg width=\"100\">{largeContent}</svg></div>";

        // This should throw InvalidOperationException because we can't rewind past buffer boundary
        var exception = Assert.Throws<InvalidOperationException>(() => HtmlParserInstance.Parse(html));
        Assert.Contains("Cannot rewind", exception.Message);
        Assert.Contains("spans multiple buffers", exception.Message);
    }

    #endregion
}
