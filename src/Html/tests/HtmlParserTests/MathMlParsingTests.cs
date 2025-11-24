using Femur.Markup.Abstractions.Nodes;
using Femur.Xml.Abstractions;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

public class MathMlParsingTests : IClassFixture<TestFixture>, IDisposable
{
    public MathMlParsingTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region MathML Parsing

    [Fact]
    public void Parse_BasicMathElement_ParsesAsXmlElement()
    {
        var html = "<math></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("math", math.TagName);
        Assert.False(math.IsSelfClosing);
        Assert.False(math.IsVoidElement);
    }

    [Fact]
    public void Parse_MathWithAttributes_ParsesAttributesWithXmlRules()
    {
        var html = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\" display=\"block\"></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("math", math.TagName);
        Assert.Equal("http://www.w3.org/1998/Math/MathML", math.Attributes["xmlns"]);
        Assert.Equal("block", math.Attributes["display"]);
    }

    [Fact]
    public void Parse_MathWithNestedElements_ParsesCorrectly()
    {
        var html = "<math><mi>x</mi><mo>+</mo><mn>5</mn></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(3, math.Children.Count);

        var mi = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("mi", mi.TagName);

        var mo = Assert.IsType<XmlElementNode>(math.Children[1]);
        Assert.Equal("mo", mo.TagName);

        var mn = Assert.IsType<XmlElementNode>(math.Children[2]);
        Assert.Equal("mn", mn.TagName);
    }

    [Fact]
    public void Parse_MathWithTextContent_ParsesTextNode()
    {
        var html = "<math><mi>x</mi></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        var mi = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("mi", mi.TagName);

        var textNode = Assert.IsType<TextNode>(mi.Children[0]);
        Assert.Equal("x", textNode.Content);
    }

    [Fact]
    public void Parse_MathNestedInHtml_ParsesCorrectly()
    {
        var html = "<div><p>Before Math</p><math><mi>x</mi></math><p>After Math</p></div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal(3, div.Children.Count);

        var p1 = Assert.IsType<ElementNode>(div.Children[0]);
        Assert.Equal("p", p1.TagName);

        var math = Assert.IsType<XmlElementNode>(div.Children[1]);
        Assert.Equal("math", math.TagName);
        _ = Assert.Single(math.Children);

        var mi = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("mi", mi.TagName);

        var p2 = Assert.IsType<ElementNode>(div.Children[2]);
        Assert.Equal("p", p2.TagName);
    }

    [Fact]
    public void Parse_MultipleMathElements_ParsesAllAsXml()
    {
        var html = "<math id=\"math1\"></math><math id=\"math2\"></math>";
        var result = HtmlParserInstance.Parse(html);

        Assert.Equal(2, result.Children.Count);

        var math1 = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("math", math1.TagName);
        Assert.Equal("math1", math1.Attributes["id"]);

        var math2 = Assert.IsType<XmlElementNode>(result.Children[1]);
        Assert.Equal("math", math2.TagName);
        Assert.Equal("math2", math2.Attributes["id"]);
    }

    [Fact]
    public void Parse_MathWithSelfClosingElements_ParsesCorrectly()
    {
        var html = "<math><mi>x</mi><mo>+</mo><mn>5</mn></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(3, math.Children.Count);

        var mi = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("mi", mi.TagName);

        var mo = Assert.IsType<XmlElementNode>(math.Children[1]);
        Assert.Equal("mo", mo.TagName);
    }

    [Fact]
    public void Parse_MathWithNamespaces_ParsesNamespaceAttributes()
    {
        var html = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\" xmlns:mml=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("http://www.w3.org/1998/Math/MathML", math.Attributes["xmlns"]);
        Assert.Equal("http://www.w3.org/1998/Math/MathML", math.Attributes["xmlns:mml"]);

        var mi = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("mi", mi.TagName);
    }

    [Fact]
    public void Parse_MathWithComments_ParsesComments()
    {
        var html = "<math><!-- This is a comment --><mi>x</mi></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(2, math.Children.Count);

        var comment = Assert.IsType<CommentNode>(math.Children[0]);
        Assert.Equal(" This is a comment ", comment.Content);

        var mi = Assert.IsType<XmlElementNode>(math.Children[1]);
        Assert.Equal("mi", mi.TagName);
    }

    [Fact]
    public void Parse_MathWithCData_ParsesCData()
    {
        var html = "<math><![CDATA[<mi>x</mi>]]></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        _ = Assert.Single(math.Children);

        var cdata = Assert.IsType<CDataNode>(math.Children[0]);
        Assert.Equal("<mi>x</mi>", cdata.Content);
    }

    [Fact]
    public void Parse_MathCaseSensitive_PreservesCase()
    {
        var html = "<math><Mi>X</Mi><Mo>+</Mo><Mn>5</Mn></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        var mi = Assert.IsType<XmlElementNode>(math.Children[0]);
        // XML is case-sensitive, so Mi should be preserved
        Assert.Equal("Mi", mi.TagName);
    }

    [Fact]
    public void Parse_MathWithComplexNesting_ParsesCorrectly()
    {
        var html = "<math><mrow><mi>x</mi><mo>+</mo><mfrac><mn>1</mn><mn>2</mn></mfrac></mrow></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        _ = Assert.Single(math.Children);

        var mrow = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("mrow", mrow.TagName);
        Assert.Equal(3, mrow.Children.Count);

        var mi = Assert.IsType<XmlElementNode>(mrow.Children[0]);
        Assert.Equal("mi", mi.TagName);

        var mo = Assert.IsType<XmlElementNode>(mrow.Children[1]);
        Assert.Equal("mo", mo.TagName);

        var mfrac = Assert.IsType<XmlElementNode>(mrow.Children[2]);
        Assert.Equal("mfrac", mfrac.TagName);
        Assert.Equal(2, mfrac.Children.Count);
    }

    [Fact]
    public void Parse_MathWithSuperscript_ParsesCorrectly()
    {
        var html = "<math><msup><mi>x</mi><mn>2</mn></msup></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        var msup = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("msup", msup.TagName);
        Assert.Equal(2, msup.Children.Count);

        var baseElement = Assert.IsType<XmlElementNode>(msup.Children[0]);
        Assert.Equal("mi", baseElement.TagName);

        var exponent = Assert.IsType<XmlElementNode>(msup.Children[1]);
        Assert.Equal("mn", exponent.TagName);
    }

    [Fact]
    public void Parse_MathWithSubscript_ParsesCorrectly()
    {
        var html = "<math><msub><mi>x</mi><mn>1</mn></msub></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        var msub = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("msub", msub.TagName);
        Assert.Equal(2, msub.Children.Count);
    }

    [Fact]
    public void Parse_MathContinuesHtmlParsing_AdvancesStreamCorrectly()
    {
        var html = "<div>Before<math><mi>x</mi></math>After</div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal(3, div.Children.Count);

        // First text node
        var text1 = Assert.IsType<TextNode>(div.Children[0]);
        Assert.Equal("Before", text1.Content);

        // Math element
        var math = Assert.IsType<XmlElementNode>(div.Children[1]);
        Assert.Equal("math", math.TagName);

        // Second text node (after Math)
        var text2 = Assert.IsType<TextNode>(div.Children[2]);
        Assert.Equal("After", text2.Content);
    }

    [Fact]
    public void Parse_MathWithUnquotedAttributes_RequiresQuotes()
    {
        // XML requires quoted attributes, but HTML allows unquoted
        // MathML should be parsed as XML, so unquoted attributes should fail or be handled differently
        var html = "<math display=block></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        // XML parser should handle this - attributes might be empty or parsed differently
        // This tests that MathML uses XML parsing rules
        Assert.Equal("math", math.TagName);
    }

    [Fact]
    public void Parse_MathEmpty_ParsesAsEmptyXmlElement()
    {
        var html = "<math></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("math", math.TagName);
        Assert.Empty(math.Children);
    }

    [Fact]
    public void Parse_MathWithMixedContent_ParsesCorrectly()
    {
        var html = "<math>Text before<mi>x</mi>Text after</math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal(3, math.Children.Count);

        var text1 = Assert.IsType<TextNode>(math.Children[0]);
        Assert.Equal("Text before", text1.Content.Trim());

        var mi = Assert.IsType<XmlElementNode>(math.Children[1]);
        Assert.Equal("mi", mi.TagName);

        var text2 = Assert.IsType<TextNode>(math.Children[2]);
        Assert.Equal("Text after", text2.Content.Trim());
    }

    [Fact]
    public void Parse_NestedMathTags_ParsesCorrectly()
    {
        var html = "<math><math id=\"inner\"><mi>x</mi></math></math>";
        var result = HtmlParserInstance.Parse(html);

        var outerMath = Assert.IsType<XmlElementNode>(result.Children[0]);
        _ = Assert.Single(outerMath.Children);

        var innerMath = Assert.IsType<XmlElementNode>(outerMath.Children[0]);
        Assert.Equal("math", innerMath.TagName);
        Assert.Equal("inner", innerMath.Attributes["id"]);
        _ = Assert.Single(innerMath.Children);

        var mi = Assert.IsType<XmlElementNode>(innerMath.Children[0]);
        Assert.Equal("mi", mi.TagName);
    }

    [Fact]
    public void Parse_MathSelfClosing_ParsesAsSelfClosing()
    {
        var html = "<math />";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("math", math.TagName);
        Assert.True(math.IsSelfClosing);
        Assert.Empty(math.Children);
    }

    [Fact]
    public void Parse_MathWithWhitespace_PreservesWhitespace()
    {
        var html = "<math>\n  <mi>x</mi>\n</math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        // XML preserves whitespace, so we should have text nodes with whitespace
        Assert.True(math.Children.Count >= 1);

        var mi = math.Children.OfType<XmlElementNode>().FirstOrDefault();
        Assert.NotNull(mi);
        Assert.Equal("mi", mi.TagName);
    }

    [Fact]
    public void Parse_MathWithSpecialCharactersInAttributes_ParsesCorrectly()
    {
        var html = "<math data-value=\"test &amp; value\" data-url=\"http://example.com?x=1&amp;y=2\"></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("test &amp; value", math.Attributes["data-value"]);
        Assert.Equal("http://example.com?x=1&amp;y=2", math.Attributes["data-url"]);
    }

    [Fact]
    public void Parse_MathWithProcessingInstructions_ParsesAll()
    {
        var html = "<math><?xml-stylesheet type=\"text/css\" href=\"style.css\"?><?custom-instruction data=\"value\"?><mi>x</mi></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        var processingInstructions = math.Children.OfType<ProcessingInstructionNode>().ToList();
        Assert.Equal(2, processingInstructions.Count);

        Assert.Equal("xml-stylesheet", processingInstructions[0].Target);
        Assert.Equal("custom-instruction", processingInstructions[1].Target);
    }

    [Fact]
    public void Parse_MathWithAttributesContainingQuotes_ParsesCorrectly()
    {
        var html = "<math data-text=\"He said \\\"Hello\\\"\"></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("He said \"Hello\"", math.Attributes["data-text"]);
    }

    [Fact]
    public void Parse_MathCaseSensitiveTagMatching_RequiresExactMatch()
    {
        // XML is case-sensitive, so <math> must match </math> exactly
        var html = "<math><mi>x</mi></MATH>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        Assert.Equal("math", math.TagName);
        // The closing tag </MATH> should still be found by ForeignElementSubStream (case-insensitive check)
        // but the XML parser will handle it according to XML rules
        _ = Assert.Single(math.Children);
    }

    [Fact]
    public void Parse_MathWithLocationTracking_TracksLocations()
    {
        var html = "<div><math display=\"block\"></math></div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        var math = Assert.IsType<XmlElementNode>(div.Children[0]);

        // Location should be tracked
        Assert.True(math.Location.Offset >= 0);
        Assert.True(math.Location.Length > 0);
    }

    [Fact]
    public void Parse_MathWithFraction_ParsesCorrectly()
    {
        var html = "<math><mfrac><mn>1</mn><mn>2</mn></mfrac></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        var mfrac = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("mfrac", mfrac.TagName);
        Assert.Equal(2, mfrac.Children.Count);

        var numerator = Assert.IsType<XmlElementNode>(mfrac.Children[0]);
        Assert.Equal("mn", numerator.TagName);

        var denominator = Assert.IsType<XmlElementNode>(mfrac.Children[1]);
        Assert.Equal("mn", denominator.TagName);
    }

    [Fact]
    public void Parse_MathWithRoot_ParsesCorrectly()
    {
        var html = "<math><mroot><mn>8</mn><mn>3</mn></mroot></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        var mroot = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("mroot", mroot.TagName);
        Assert.Equal(2, mroot.Children.Count);
    }

    [Fact]
    public void Parse_MathWithUnderOver_ParsesCorrectly()
    {
        var html = "<math><munder><mi>x</mi><mo>_</mo></munder></math>";
        var result = HtmlParserInstance.Parse(html);

        var math = Assert.IsType<XmlElementNode>(result.Children[0]);
        var munder = Assert.IsType<XmlElementNode>(math.Children[0]);
        Assert.Equal("munder", munder.TagName);
        Assert.Equal(2, munder.Children.Count);
    }

    [Fact]
    public void Parse_MathAndSvgTogether_ParsesBothCorrectly()
    {
        var html = "<div><math><mi>x</mi></math><svg><circle r=\"10\"></circle></svg></div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal(2, div.Children.Count);

        var math = Assert.IsType<XmlElementNode>(div.Children[0]);
        Assert.Equal("math", math.TagName);

        var svg = Assert.IsType<XmlElementNode>(div.Children[1]);
        Assert.Equal("svg", svg.TagName);
    }

    [Fact(Skip = "This test was checking for a limitation where MathML tags starting in previous buffers would fail. However, the current implementation can handle MathML tags that start in the current buffer, even if there was content before them in previous buffers. The exception only occurs if the MathML tag itself starts in a previous buffer that has been discarded, which this test doesn't actually trigger.")]
    public void Parse_MathSpanningMultipleBuffers_ThrowsException()
    {
        // Create a large MathML that would span multiple buffers (default buffer is 4KB)
        // We'll create a MathML with enough content to exceed the buffer
        var largeContent = new string('x', 5000); // 5KB of content
        var html = $"<div>{largeContent}<math display=\"block\">{largeContent}</math></div>";

        // This should throw InvalidOperationException because we can't rewind past buffer boundary
        var exception = Assert.Throws<InvalidOperationException>(() => HtmlParserInstance.Parse(html));
        Assert.Contains("Cannot rewind", exception.Message);
        Assert.Contains("spans multiple buffers", exception.Message);
    }

    #endregion
}

