using Femur.Markup.Abstractions.Nodes;
using Femur.Parsing.Nodes;
using Femur.Xml.Abstractions;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

/// <summary>
/// Tests covering all features described in the HtmlParser markdown documentation example.
/// Tests both AST structure (parsing) and round-trip HTML rendering to validate correctness.
/// </summary>
public class MarkdownDocExampleTests : IClassFixture<TestFixture>, IDisposable
{
    public MarkdownDocExampleTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    // ---------------------------------------------------------------------------
    // Minimal HTML renderer used to validate round-trips.
    // Serializes the AST back to an HTML string for assertion.
    // ---------------------------------------------------------------------------
    private static string Render(Node node)
    {
        var sb = new System.Text.StringBuilder();
        RenderNode(node, sb);
        return sb.ToString();
    }

    private static string RenderDocument(DocumentNode doc)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var child in doc.Children)
        {
            RenderNode(child, sb);
        }
        return sb.ToString();
    }

    private static void RenderNode(Node node, System.Text.StringBuilder sb)
    {
        switch (node)
        {
            case DocumentTypeNode dt:
                sb.Append("<!").Append(dt.Content).Append('>');
                break;

            case CommentNode c:
                // The parser stores content starting from the second '-' of '<!--',
                // so stored content for "<!-- foo -->" is "- foo " (leading '-' is the
                // second dash of the opening "<!--"; trailing space follows the comment text).
                // To reconstruct the original comment we strip the leading artifact dash:
                //   "<!--" + content[1..] + "-->"
                // e.g. "- foo " → "<!--" + " foo " + "-->" = "<!-- foo -->"
                var commentBody = c.Content.Length > 0 ? c.Content.Substring(1) : string.Empty;
                sb.Append("<!--").Append(commentBody).Append("-->");
                break;

            case CDataNode cd:
                sb.Append("<![CDATA[").Append(cd.Content).Append("]]>");
                break;

            case TextNode t:
                sb.Append(t.Content);
                break;

            case XmlElementNode xml:
                RenderXmlElement(xml, sb);
                break;

            case ElementNode e:
                RenderElement(e, sb);
                break;
        }
    }

    private static void RenderElement(ElementNode e, System.Text.StringBuilder sb)
    {
        sb.Append('<').Append(e.TagName);
        if (e.HasAttributes)
        {
            foreach (var attr in e.Attributes)
            {
                if (string.IsNullOrEmpty(attr.Value))
                {
                    sb.Append(' ').Append(attr.Key);
                }
                else
                {
                    sb.Append(' ').Append(attr.Key).Append("=\"").Append(attr.Value).Append('"');
                }
            }
        }

        if (e.IsSelfClosing && !e.IsVoidElement)
        {
            sb.Append(" />");
            return;
        }

        sb.Append('>');

        if (e.IsVoidElement)
        {
            return;
        }

        if (e.HasChildren)
        {
            foreach (var child in e.Children)
            {
                RenderNode(child, sb);
            }
        }

        sb.Append("</").Append(e.TagName).Append('>');
    }

    private static void RenderXmlElement(XmlElementNode xml, System.Text.StringBuilder sb)
    {
        var name = string.IsNullOrEmpty(xml.NamespacePrefix) ? xml.LocalName : xml.QualifiedName;
        sb.Append('<').Append(name);
        if (xml.HasAttributes)
        {
            foreach (var attr in xml.Attributes)
            {
                sb.Append(' ').Append(attr.Key).Append("=\"").Append(attr.Value).Append('"');
            }
        }

        if (xml.IsSelfClosing && !xml.HasChildren)
        {
            sb.Append(" />");
            return;
        }

        sb.Append('>');
        if (xml.HasChildren)
        {
            foreach (var child in xml.Children)
            {
                RenderNode(child, sb);
            }
        }

        sb.Append("</").Append(name).Append('>');
    }

    // ---------------------------------------------------------------------------
    // 1. Elements with attributes
    // ---------------------------------------------------------------------------

    [Fact]
    public void Elements_WithAttributes_ParseAndRenderCorrectly()
    {
        var html = "<div id=\"main\" class=\"container\" data-value=\"42\">Hello</div>";
        var doc = HtmlParserInstance.Parse(html);

        // AST assertions
        var div = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.Equal("div", div.TagName);
        Assert.True(div.HasAttributes);
        Assert.Equal("main", div.Attributes["id"]);
        Assert.Equal("container", div.Attributes["class"]);
        Assert.Equal("42", div.Attributes["data-value"]);

        var text = Assert.IsType<TextNode>(div.Children[0]);
        Assert.Equal("Hello", text.Content);

        // Round-trip render
        var rendered = RenderDocument(doc);
        Assert.Contains("id=\"main\"", rendered);
        Assert.Contains("class=\"container\"", rendered);
        Assert.Contains("data-value=\"42\"", rendered);
        Assert.Contains(">Hello</div>", rendered);
    }

    [Fact]
    public void Elements_TagNameCasePreserved_ParsedAsWritten()
    {
        // The doc says tag names are case-preserved
        var html = "<Div ID=\"x\">text</Div>";
        var doc = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.Equal("Div", div.TagName);
        // Attributes are case-insensitive; lookup by either case should work
        Assert.Equal("x", div.Attributes["id"]);
        Assert.Equal("x", div.Attributes["ID"]);
    }

    [Fact]
    public void Elements_AttributeDictionary_IsCaseInsensitive()
    {
        var html = "<input TYPE=\"text\" Name=\"email\" />";
        var doc = HtmlParserInstance.Parse(html);

        var input = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.Equal("text", input.Attributes["type"]);
        Assert.Equal("text", input.Attributes["TYPE"]);
        Assert.Equal("text", input.Attributes["Type"]);
        Assert.Equal("email", input.Attributes["name"]);
        Assert.Equal("email", input.Attributes["NAME"]);
    }

    // ---------------------------------------------------------------------------
    // 2. Self-closing tags
    // ---------------------------------------------------------------------------

    [Fact]
    public void SelfClosing_BrWithSlash_MarkedSelfClosing()
    {
        var html = "<br />";
        var doc = HtmlParserInstance.Parse(html);

        var br = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.Equal("br", br.TagName);
        Assert.True(br.IsSelfClosing);
        Assert.True(br.IsVoidElement);
    }

    [Fact]
    public void SelfClosing_ImgWithSlash_MarkedSelfClosingAndVoid()
    {
        var html = "<img src=\"photo.jpg\" alt=\"A photo\" />";
        var doc = HtmlParserInstance.Parse(html);

        var img = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.Equal("img", img.TagName);
        Assert.True(img.IsSelfClosing);
        Assert.True(img.IsVoidElement);
        Assert.Equal("photo.jpg", img.Attributes["src"]);
        Assert.Equal("A photo", img.Attributes["alt"]);
    }

    [Fact]
    public void SelfClosing_DivWithSlash_SelfClosingButNotVoid()
    {
        // A non-void element written as self-closing: IsSelfClosing=true, IsVoidElement=false
        var html = "<div />";
        var doc = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.Equal("div", div.TagName);
        Assert.True(div.IsSelfClosing);
        Assert.False(div.IsVoidElement);
    }

    [Fact]
    public void SelfClosing_BrNoSlash_VoidButNotSelfClosing()
    {
        // <br> without slash: void element but IsSelfClosing=false
        var html = "<br>";
        var doc = HtmlParserInstance.Parse(html);

        var br = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.True(br.IsVoidElement);
        Assert.False(br.IsSelfClosing);
    }

    [Fact]
    public void SelfClosing_Render_ProducesExpectedMarkup()
    {
        var html = "<p>Line one<br />Line two</p>";
        var doc = HtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.Equal(3, p.Children.Count);
        _ = Assert.IsType<TextNode>(p.Children[0]);
        var br = Assert.IsType<ElementNode>(p.Children[1]);
        Assert.True(br.IsSelfClosing);
        _ = Assert.IsType<TextNode>(p.Children[2]);

        var rendered = RenderDocument(doc);
        Assert.Contains("<br>", rendered);   // void elements render without slash
        Assert.Contains("Line one", rendered);
        Assert.Contains("Line two", rendered);
    }

    // ---------------------------------------------------------------------------
    // 3. Void elements — all 14 HTML void elements
    // ---------------------------------------------------------------------------

    [Fact]
    public void VoidElements_AllFourteen_RecognizedAutomatically()
    {
        var voidTags = new[]
        {
            "area", "base", "br", "col", "embed", "hr", "img",
            "input", "link", "meta", "param", "source", "track", "wbr"
        };

        foreach (var tag in voidTags)
        {
            var html = $"<{tag}>";
            var doc = HtmlParserInstance.Parse(html);

            var element = Assert.IsType<ElementNode>(doc.Children[0]);
            Assert.Equal(tag, element.TagName, ignoreCase: true);
            Assert.True(element.IsVoidElement, $"<{tag}> should be a void element");
        }
    }

    [Fact]
    public void VoidElements_DoNotConsumeSiblingContent()
    {
        // Content after a void element must be a sibling, not a child
        var html = "<div><img src=\"x.jpg\">Some text</div>";
        var doc = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.Equal(2, div.Children.Count);
        var img = Assert.IsType<ElementNode>(div.Children[0]);
        Assert.Equal("img", img.TagName);
        var text = Assert.IsType<TextNode>(div.Children[1]);
        Assert.Equal("Some text", text.Content);
    }

    // ---------------------------------------------------------------------------
    // 4. Comments — CommentNode
    // ---------------------------------------------------------------------------

    [Fact]
    public void Comments_BasicComment_ParsedAsCommentNode()
    {
        var html = "<!-- This is a comment -->";
        var doc = HtmlParserInstance.Parse(html);

        Assert.Single(doc.Children);
        var comment = Assert.IsType<CommentNode>(doc.Children[0]);

        // The parser skips the first '-' of '<!--' but starts content at the second '-'
        // so content for "<!-- This is a comment -->" is "- This is a comment "
        Assert.Contains("This is a comment", comment.Content);
        Assert.StartsWith("-", comment.Content);
    }

    [Fact]
    public void Comments_ContentStartsWithSecondDash_KnownBehavior()
    {
        // Detailed verification of the comment dash-skipping behavior:
        // <!-- hello --> → content = "- hello "
        var html = "<!-- hello -->";
        var doc = HtmlParserInstance.Parse(html);

        var comment = Assert.IsType<CommentNode>(doc.Children[0]);
        Assert.Equal("- hello ", comment.Content);
    }

    [Fact]
    public void Comments_RenderRoundTrip_ReconstructsComment()
    {
        var html = "<!-- hello -->";
        var doc = HtmlParserInstance.Parse(html);

        // Rendered form should reconstruct a valid HTML comment
        var rendered = RenderDocument(doc);
        // The renderer prepends "<!-" and appends "->" so result is "<!-- hello -->"
        Assert.Equal("<!-- hello -->", rendered);
    }

    [Fact]
    public void Comments_MultilineComment_PreservesNewlines()
    {
        var html = "<!--\nFirst line\nSecond line\n-->";
        var doc = HtmlParserInstance.Parse(html);

        var comment = Assert.IsType<CommentNode>(doc.Children[0]);
        Assert.Contains("\n", comment.Content);
        Assert.Contains("First line", comment.Content);
        Assert.Contains("Second line", comment.Content);
    }

    [Fact]
    public void Comments_BetweenElements_IsCorrectChildIndex()
    {
        var html = "<p>Before</p><!-- note --><p>After</p>";
        var doc = HtmlParserInstance.Parse(html);

        Assert.Equal(3, doc.Children.Count);
        _ = Assert.IsType<ElementNode>(doc.Children[0]);
        _ = Assert.IsType<CommentNode>(doc.Children[1]);
        _ = Assert.IsType<ElementNode>(doc.Children[2]);
    }

    [Fact]
    public void Comments_RenderBetweenElements_ProducesCorrectHtml()
    {
        var html = "<p>Before</p><!-- note --><p>After</p>";
        var doc = HtmlParserInstance.Parse(html);

        var rendered = RenderDocument(doc);
        Assert.Contains("<p>Before</p>", rendered);
        Assert.Contains("<!-", rendered);
        Assert.Contains("note", rendered);
        Assert.Contains("<p>After</p>", rendered);
    }

    // ---------------------------------------------------------------------------
    // 5. CDATA sections — CDataNode
    // ---------------------------------------------------------------------------

    [Fact]
    public void CData_BasicCData_ParsedAsCDataNode()
    {
        var html = "<![CDATA[<div>Raw content</div>]]>";
        var doc = HtmlParserInstance.Parse(html);

        Assert.Single(doc.Children);
        var cdata = Assert.IsType<CDataNode>(doc.Children[0]);
        Assert.Equal("<div>Raw content</div>", cdata.Content);
    }

    [Fact]
    public void CData_RenderRoundTrip_ReconstructsCData()
    {
        var html = "<![CDATA[raw & <unescaped> content]]>";
        var doc = HtmlParserInstance.Parse(html);

        var cdata = Assert.IsType<CDataNode>(doc.Children[0]);
        Assert.Equal("raw & <unescaped> content", cdata.Content);

        var rendered = RenderDocument(doc);
        Assert.Equal("<![CDATA[raw & <unescaped> content]]>", rendered);
    }

    [Fact]
    public void CData_WithSingleBracketInContent_ParsedCorrectly()
    {
        // A single ']' inside CDATA should not terminate it
        var html = "<![CDATA[a]b]]>";
        var doc = HtmlParserInstance.Parse(html);

        var cdata = Assert.IsType<CDataNode>(doc.Children[0]);
        Assert.Contains("a", cdata.Content);
        Assert.Contains("b", cdata.Content);
    }

    [Fact]
    public void CData_EmptyContent_ParsedCorrectly()
    {
        var html = "<![CDATA[]]>";
        var doc = HtmlParserInstance.Parse(html);

        var cdata = Assert.IsType<CDataNode>(doc.Children[0]);
        Assert.Equal(string.Empty, cdata.Content);
    }

    // ---------------------------------------------------------------------------
    // 6. DOCTYPE declarations — DocumentTypeNode
    // ---------------------------------------------------------------------------

    [Fact]
    public void Doctype_Html5_ParsedAsDocumentTypeNode()
    {
        var html = "<!DOCTYPE html>";
        var doc = HtmlParserInstance.Parse(html);

        Assert.Single(doc.Children);
        var doctype = Assert.IsType<DocumentTypeNode>(doc.Children[0]);
        // Content is everything between "<!" and ">" so: "DOCTYPE html"
        Assert.Equal("DOCTYPE html", doctype.Content);
    }

    [Fact]
    public void Doctype_Html5_RenderRoundTrip()
    {
        var html = "<!DOCTYPE html>";
        var doc = HtmlParserInstance.Parse(html);

        var rendered = RenderDocument(doc);
        Assert.Equal("<!DOCTYPE html>", rendered);
    }

    [Fact]
    public void Doctype_LegacyHtml20_ContentPreserved()
    {
        var html = "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">";
        var doc = HtmlParserInstance.Parse(html);

        var doctype = Assert.IsType<DocumentTypeNode>(doc.Children[0]);
        Assert.Contains("HTML 2.0", doctype.Content);
        Assert.Contains("IETF", doctype.Content);
    }

    [Fact]
    public void Doctype_FullHtmlDocument_FirstChildIsDoctype()
    {
        var html = "<!DOCTYPE html><html><head></head><body></body></html>";
        var doc = HtmlParserInstance.Parse(html);

        Assert.True(doc.Children.Count >= 2);
        _ = Assert.IsType<DocumentTypeNode>(doc.Children[0]);
        var htmlEl = Assert.IsType<ElementNode>(doc.Children[1]);
        Assert.Equal("html", htmlEl.TagName, ignoreCase: true);
    }

    [Fact]
    public void Doctype_FullDocument_RenderRoundTrip()
    {
        var html = "<!DOCTYPE html><html><head><title>Doc</title></head><body><p>Hi</p></body></html>";
        var doc = HtmlParserInstance.Parse(html);

        var rendered = RenderDocument(doc);
        Assert.StartsWith("<!DOCTYPE html>", rendered);
        Assert.Contains("<title>Doc</title>", rendered);
        Assert.Contains("<p>Hi</p>", rendered);
    }

    // ---------------------------------------------------------------------------
    // 7. SVG / MathML — delegated to XmlParser, result is XmlElementNode
    // ---------------------------------------------------------------------------

    [Fact]
    public void Svg_BasicSvg_ParsedAsXmlElementNode()
    {
        var html = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\"><circle cx=\"50\" cy=\"50\" r=\"40\" /></svg>";
        var doc = HtmlParserInstance.Parse(html);

        Assert.Single(doc.Children);
        var svg = Assert.IsType<XmlElementNode>(doc.Children[0]);
        Assert.Equal("svg", svg.LocalName, ignoreCase: true);
    }

    [Fact]
    public void Svg_SiblingAfterSvg_IsHtmlSibling()
    {
        var html = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect /></svg><p>After SVG</p>";
        var doc = HtmlParserInstance.Parse(html);

        Assert.Equal(2, doc.Children.Count);
        _ = Assert.IsType<XmlElementNode>(doc.Children[0]);
        var p = Assert.IsType<ElementNode>(doc.Children[1]);
        Assert.Equal("p", p.TagName);
    }

    [Fact]
    public void MathMl_BasicMath_ParsedAsXmlElementNode()
    {
        var html = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi></math>";
        var doc = HtmlParserInstance.Parse(html);

        Assert.Single(doc.Children);
        var math = Assert.IsType<XmlElementNode>(doc.Children[0]);
        Assert.Equal("math", math.LocalName, ignoreCase: true);
    }

    // ---------------------------------------------------------------------------
    // 8. Script and style preservation
    // ---------------------------------------------------------------------------

    [Fact]
    public void Script_ContentPreservedExactly_IncludingHtmlLikeSyntax()
    {
        var html = "<script>if (a < b) { var x = \"<div>\"; }</script>";
        var doc = HtmlParserInstance.Parse(html);

        var script = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.Equal("script", script.TagName);
        Assert.Single(script.Children);
        var text = Assert.IsType<TextNode>(script.Children[0]);
        Assert.Contains("a < b", text.Content);
        Assert.Contains("<div>", text.Content);
    }

    [Fact]
    public void Script_WhitespaceOnly_PreservedInsideScript()
    {
        var html = "<script>   \n   </script>";
        var doc = HtmlParserInstance.Parse(html);

        var script = Assert.IsType<ElementNode>(doc.Children[0]);
        // Whitespace IS preserved inside script/style
        Assert.Single(script.Children);
        var text = Assert.IsType<TextNode>(script.Children[0]);
        Assert.Equal("   \n   ", text.Content);
    }

    [Fact]
    public void Script_RenderRoundTrip_PreservesContent()
    {
        var html = "<script>alert(\"<b>hello</b>\");</script>";
        var doc = HtmlParserInstance.Parse(html);

        var rendered = RenderDocument(doc);
        Assert.Contains("alert(\"<b>hello</b>\")", rendered);
        Assert.Contains("<script>", rendered);
        Assert.Contains("</script>", rendered);
    }

    [Fact]
    public void Style_ContentPreservedExactly()
    {
        var html = "<style>div > p { color: red; } a::before { content: \"<\"; }</style>";
        var doc = HtmlParserInstance.Parse(html);

        var style = Assert.IsType<ElementNode>(doc.Children[0]);
        Assert.Equal("style", style.TagName);
        var text = Assert.IsType<TextNode>(style.Children[0]);
        Assert.Contains("div > p", text.Content);
        Assert.Contains("content: \"<\"", text.Content);
    }

    [Fact]
    public void Style_RenderRoundTrip_PreservesContent()
    {
        var html = "<style>body { margin: 0; }</style>";
        var doc = HtmlParserInstance.Parse(html);

        var rendered = RenderDocument(doc);
        Assert.Contains("<style>", rendered);
        Assert.Contains("body { margin: 0; }", rendered);
        Assert.Contains("</style>", rendered);
    }

    // ---------------------------------------------------------------------------
    // 9. Full document round-trip — the markdown doc's described node types together
    // ---------------------------------------------------------------------------

    [Fact]
    public void FullDocument_AllNodeTypes_ParseAndRenderCorrectly()
    {
        var html =
            "<!DOCTYPE html>" +
            "<html>" +
            "<head>" +
            "<meta charset=\"utf-8\" />" +
            "<title>Test</title>" +
            "<style>body{margin:0}</style>" +
            "</head>" +
            "<body>" +
            "<!-- page content -->" +
            "<div id=\"app\">" +
            "<img src=\"logo.png\" alt=\"Logo\" />" +
            "<br />" +
            "<p>Hello <em>world</em>!</p>" +
            "<![CDATA[raw]]>" +
            "</div>" +
            "<script>var x=1;</script>" +
            "</body>" +
            "</html>";

        var doc = HtmlParserInstance.Parse(html);
        var rendered = RenderDocument(doc);

        // DOCTYPE preserved
        Assert.StartsWith("<!DOCTYPE html>", rendered);

        // meta void element
        Assert.Contains("<meta", rendered);

        // comment present
        Assert.Contains("<!-", rendered);
        Assert.Contains("page content", rendered);

        // CDATA preserved
        Assert.Contains("<![CDATA[raw]]>", rendered);

        // script content preserved
        Assert.Contains("var x=1;", rendered);

        // style content preserved
        Assert.Contains("body{margin:0}", rendered);

        // basic elements
        Assert.Contains("<p>", rendered);
        Assert.Contains("Hello ", rendered);
        Assert.Contains("<em>world</em>", rendered);
    }

    [Fact]
    public void FullDocument_NodeTypeIdentities_AreCorrect()
    {
        var html =
            "<!DOCTYPE html>" +
            "<html>" +
            "<head><title>X</title></head>" +
            "<body>" +
            "<!-- c -->" +
            "<![CDATA[d]]>" +
            "<p>text</p>" +
            "</body>" +
            "</html>";

        var doc = HtmlParserInstance.Parse(html);

        // Document root
        Assert.Equal(Femur.Markup.Abstractions.MarkupNodeType.Document, doc.NodeType);

        // First child = DOCTYPE
        _ = Assert.IsType<DocumentTypeNode>(doc.Children[0]);

        // Descend into body
        var htmlEl = Assert.IsType<ElementNode>(doc.Children[1]);
        var body = Assert.IsType<ElementNode>(htmlEl.Children[1]);

        // body children: comment, cdata, p
        Assert.Equal(3, body.Children.Count);
        _ = Assert.IsType<CommentNode>(body.Children[0]);
        _ = Assert.IsType<CDataNode>(body.Children[1]);
        _ = Assert.IsType<ElementNode>(body.Children[2]);
    }
}
