using Femur.Markdown.Abstractions.Nodes;
using Femur.Markdown.Parser;
using Femur.Markdown.Renderer;

namespace MarkdownRendererTests;

/// <summary>
/// Tests that parse the exact markdown documentation block for the HtmlParser feature
/// description, assert the resulting AST structure, and validate HTML rendering output.
///
/// The markdown block being tested:
///
///   ### HTML Parser: Standard Markup Parsing
///   
///   The `HtmlParser` provides streaming HTML 2.0 parsing with AST generation:
///   
///   **Key features**:
///   - **Elements with attributes** - case-preserved tag names, lazy attribute dictionary
///   - **Self-closing tags** - `&lt;br /&gt;`, `&lt;img /&gt;` detected and marked
///   ...
///   
///   :::C:Codeblock {lang="csharp"}
///   // Core nodes ...
///   :::
///   
///   **Usage example**:
/// </summary>
public class HtmlParserDocBlockTests
{
    private readonly MarkdownHtmlRenderer _renderer = new();

    // The exact markdown content under test — preserved verbatim.
    private const string Markdown = """
### HTML Parser: Standard Markup Parsing

The `HtmlParser` provides streaming HTML 2.0 parsing with AST generation:

**Key features**:
- **Elements with attributes** - case-preserved tag names, lazy attribute dictionary
- **Self-closing tags** - `<br />`, `<img />` detected and marked
- **Void elements** - HTML void elements (`img`, `br`, `input`, etc.) automatically recognized
- **Comments** - `<!-- comment -->` parsed as `CommentNode`
- **CDATA sections** - `<![CDATA[...]]>` supported
- **DOCTYPE declarations** - `<!DOCTYPE html>` parsed as `DocumentTypeNode`
- **SVG/MathML** - Delegates to XML parser for proper namespace handling
- **Script/style preservation** - Content inside `<script>` and `<style>` preserved exactly

**Node types**:

:::C:Codeblock {lang="csharp"}
// Core nodes from Femur.Markup.Abstractions
DocumentNode     // Root document
ElementNode      // HTML elements (<div>, <p>, etc.)
  ├─ TagName: string
  ├─ Attributes: Dictionary<string, string>
  ├─ IsSelfClosing: bool
  └─ IsVoidElement: bool

TextNode         // Text content
CommentNode      // <!-- comments -->
CDataNode        // <![CDATA[...]]>
DocumentTypeNode // <!DOCTYPE html>
XmlElementNode   // SVG/MathML elements
:::

**Usage example**: 
""";

    private string ParseAndRender(string markdown)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));
        var parser = new MarkdownParser(stream);
        var document = parser.Parse();
        return this._renderer.Render(document);
    }

    // -------------------------------------------------------------------------
    // AST structure tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_DocBlock_DocumentHasExpectedChildCount()
    {
        var doc = MarkdownParser.Parse(Markdown);

        // Expected top-level blocks:
        // 1. HeadingNode (###)
        // 2. ParagraphNode (The `HtmlParser` provides...)
        // 3. ParagraphNode (**Key features**:)
        // 4. ListNode (bullet list)
        // 5. ParagraphNode (**Node types**:)
        // 6. FencedDivNode (:::C:Codeblock)
        // 7. ParagraphNode (**Usage example**:)
        Assert.Equal(7, doc.Children.Count);
    }

    [Fact]
    public void Parse_DocBlock_FirstChildIsH3Heading()
    {
        var doc = MarkdownParser.Parse(Markdown);

        var heading = Assert.IsType<HeadingNode>(doc.Children[0]);
        Assert.Equal(3, heading.Level);

        // Heading text content
        var text = heading.Children.OfType<MarkdownTextNode>().First();
        Assert.Contains("HTML Parser", text.Content);
        Assert.Contains("Standard Markup Parsing", text.Content);
    }

    [Fact]
    public void Parse_DocBlock_SecondChildIsIntroductionParagraph()
    {
        var doc = MarkdownParser.Parse(Markdown);

        var para = Assert.IsType<ParagraphNode>(doc.Children[1]);

        // Contains inline code: `HtmlParser`
        var code = para.Children.OfType<CodeSpanNode>().FirstOrDefault();
        Assert.NotNull(code);
        Assert.Equal("HtmlParser", code.Content);

        // Contains surrounding text
        var texts = para.Children.OfType<MarkdownTextNode>().ToList();
        Assert.Contains(texts, t => t.Content.Contains("streaming HTML"));
    }

    [Fact]
    public void Parse_DocBlock_ThirdChildIsKeyFeaturesParagraph()
    {
        var doc = MarkdownParser.Parse(Markdown);

        var para = Assert.IsType<ParagraphNode>(doc.Children[2]);

        // **Key features**: is a StrongEmphasisNode followed by a colon text node
        var strong = para.Children.OfType<StrongEmphasisNode>().FirstOrDefault();
        Assert.NotNull(strong);

        var strongText = strong.Children.OfType<MarkdownTextNode>().First();
        Assert.Equal("Key features", strongText.Content);
    }

    [Fact]
    public void Parse_DocBlock_FourthChildIsUnorderedList()
    {
        var doc = MarkdownParser.Parse(Markdown);

        var list = Assert.IsType<ListNode>(doc.Children[3]);
        Assert.False(list.IsOrdered);
        Assert.Equal(8, list.Children.Count); // 8 bullet items
    }

    [Fact]
    public void Parse_DocBlock_ListItem_ElementsWithAttributes_HasBoldAndCode()
    {
        var doc = MarkdownParser.Parse(Markdown);
        var list = Assert.IsType<ListNode>(doc.Children[3]);
        var firstItem = Assert.IsType<ListItemNode>(list.Children[0]);

        // "**Elements with attributes** - case-preserved tag names, lazy attribute dictionary"
        var para = firstItem.Children.OfType<ParagraphNode>().First();
        var strong = para.Children.OfType<StrongEmphasisNode>().FirstOrDefault();
        Assert.NotNull(strong);
        var strongText = strong.Children.OfType<MarkdownTextNode>().First();
        Assert.Equal("Elements with attributes", strongText.Content);
    }

    [Fact]
    public void Parse_DocBlock_ListItem_SelfClosingTags_HasInlineCodes()
    {
        var doc = MarkdownParser.Parse(Markdown);
        var list = Assert.IsType<ListNode>(doc.Children[3]);
        var secondItem = Assert.IsType<ListItemNode>(list.Children[1]);

        // "**Self-closing tags** - `<br />`, `<img />` detected and marked"
        var para = secondItem.Children.OfType<ParagraphNode>().First();
        var codes = para.Children.OfType<CodeSpanNode>().ToList();
        Assert.Equal(2, codes.Count);
        Assert.Equal("<br />", codes[0].Content);
        Assert.Equal("<img />", codes[1].Content);
    }

    [Fact]
    public void Parse_DocBlock_ListItem_VoidElements_HasInlineCodes()
    {
        var doc = MarkdownParser.Parse(Markdown);
        var list = Assert.IsType<ListNode>(doc.Children[3]);
        var thirdItem = Assert.IsType<ListItemNode>(list.Children[2]);

        // "**Void elements** - HTML void elements (`img`, `br`, `input`, etc.) automatically recognized"
        var para = thirdItem.Children.OfType<ParagraphNode>().First();
        var codes = para.Children.OfType<CodeSpanNode>().ToList();
        Assert.True(codes.Count >= 3);
        Assert.Contains(codes, c => c.Content == "img");
        Assert.Contains(codes, c => c.Content == "br");
        Assert.Contains(codes, c => c.Content == "input");
    }

    [Fact]
    public void Parse_DocBlock_ListItem_Comments_HasInlineCodeAndText()
    {
        var doc = MarkdownParser.Parse(Markdown);
        var list = Assert.IsType<ListNode>(doc.Children[3]);
        var fourthItem = Assert.IsType<ListItemNode>(list.Children[3]);

        // "**Comments** - `<!-- comment -->` parsed as `CommentNode`"
        var para = fourthItem.Children.OfType<ParagraphNode>().First();
        var codes = para.Children.OfType<CodeSpanNode>().ToList();
        Assert.Contains(codes, c => c.Content == "<!-- comment -->");
        Assert.Contains(codes, c => c.Content == "CommentNode");
    }

    [Fact]
    public void Parse_DocBlock_FifthChildIsNodeTypesParagraph()
    {
        var doc = MarkdownParser.Parse(Markdown);

        var para = Assert.IsType<ParagraphNode>(doc.Children[4]);
        var strong = para.Children.OfType<StrongEmphasisNode>().FirstOrDefault();
        Assert.NotNull(strong);
        var strongText = strong.Children.OfType<MarkdownTextNode>().First();
        Assert.Equal("Node types", strongText.Content);
    }

    [Fact]
    public void Parse_DocBlock_SixthChildIsFencedDiv()
    {
        var doc = MarkdownParser.Parse(Markdown);

        var div = Assert.IsType<FencedDivNode>(doc.Children[5]);
        Assert.Equal("C:Codeblock", div.Tag);
    }

    [Fact]
    public void Parse_DocBlock_FencedDiv_HasCSharpLangAttribute()
    {
        var doc = MarkdownParser.Parse(Markdown);
        var div = Assert.IsType<FencedDivNode>(doc.Children[5]);

        Assert.Equal("{lang=\"csharp\"}", div.Attributes);
        Assert.Equal("csharp", div.ParsedAttributes.KeyValueAttributes["lang"]);
    }

    [Fact]
    public void Parse_DocBlock_FencedDiv_IsRawMode_NoChildren()
    {
        // C:Codeblock uses the colon convention — content is not recursively parsed
        var doc = MarkdownParser.Parse(Markdown);
        var div = Assert.IsType<FencedDivNode>(doc.Children[5]);

        Assert.False(div.HasChildren);
    }

    [Fact]
    public void Parse_DocBlock_FencedDiv_RawContent_ContainsNodeTypeComments()
    {
        var doc = MarkdownParser.Parse(Markdown);
        var div = Assert.IsType<FencedDivNode>(doc.Children[5]);

        Assert.Contains("DocumentNode", div.RawContent);
        Assert.Contains("ElementNode", div.RawContent);
        Assert.Contains("TextNode", div.RawContent);
        Assert.Contains("CommentNode", div.RawContent);
        Assert.Contains("CDataNode", div.RawContent);
        Assert.Contains("DocumentTypeNode", div.RawContent);
        Assert.Contains("XmlElementNode", div.RawContent);
    }

    [Fact]
    public void Parse_DocBlock_FencedDiv_RawContent_ContainsElementNodeProperties()
    {
        var doc = MarkdownParser.Parse(Markdown);
        var div = Assert.IsType<FencedDivNode>(doc.Children[5]);

        Assert.Contains("TagName", div.RawContent);
        Assert.Contains("Attributes", div.RawContent);
        Assert.Contains("IsSelfClosing", div.RawContent);
        Assert.Contains("IsVoidElement", div.RawContent);
    }

    [Fact]
    public void Parse_DocBlock_LastChildIsUsageExampleParagraph()
    {
        var doc = MarkdownParser.Parse(Markdown);

        var para = Assert.IsType<ParagraphNode>(doc.Children[6]);
        var strong = para.Children.OfType<StrongEmphasisNode>().FirstOrDefault();
        Assert.NotNull(strong);
        var strongText = strong.Children.OfType<MarkdownTextNode>().First();
        Assert.Equal("Usage example", strongText.Content);
    }

    // -------------------------------------------------------------------------
    // HTML rendering tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_DocBlock_HeadingRendersAsH3()
    {
        var html = ParseAndRender(Markdown);

        Assert.Contains("<h3>", html);
        Assert.Contains("HTML Parser", html);
        Assert.Contains("Standard Markup Parsing", html);
        Assert.Contains("</h3>", html);
    }

    [Fact]
    public void Render_DocBlock_IntroParagraph_InlineCodeRendered()
    {
        var html = ParseAndRender(Markdown);

        // `HtmlParser` inline code
        Assert.Contains("<code>HtmlParser</code>", html);
        Assert.Contains("streaming HTML", html);
    }

    [Fact]
    public void Render_DocBlock_KeyFeatures_RenderedAsBold()
    {
        var html = ParseAndRender(Markdown);

        Assert.Contains("<strong>Key features</strong>", html);
    }

    [Fact]
    public void Render_DocBlock_ListRendersAsUl()
    {
        var html = ParseAndRender(Markdown);

        Assert.Contains("<ul>", html);
        Assert.Contains("</ul>", html);
        Assert.Contains("<li>", html);
    }

    [Fact]
    public void Render_DocBlock_ListItem_SelfClosingTags_InlineCodesEscaped()
    {
        var html = ParseAndRender(Markdown);

        // `<br />` and `<img />` — angle brackets must be HTML-escaped in output
        Assert.Contains("<code>&lt;br /&gt;</code>", html);
        Assert.Contains("<code>&lt;img /&gt;</code>", html);
    }

    [Fact]
    public void Render_DocBlock_ListItem_CommentCode_Escaped()
    {
        var html = ParseAndRender(Markdown);

        // `<!-- comment -->` inside inline code must be escaped
        Assert.Contains("<code>&lt;!-- comment --&gt;</code>", html);
        Assert.Contains("<code>CommentNode</code>", html);
    }

    [Fact]
    public void Render_DocBlock_ListItem_CDataCode_Escaped()
    {
        var html = ParseAndRender(Markdown);

        // `<![CDATA[...]]>` inside inline code
        Assert.Contains("CDATA", html);
    }

    [Fact]
    public void Render_DocBlock_ListItem_DoctypeCode_Escaped()
    {
        var html = ParseAndRender(Markdown);

        // `<!DOCTYPE html>` inside inline code
        Assert.Contains("<code>&lt;!DOCTYPE html&gt;</code>", html);
        Assert.Contains("<code>DocumentTypeNode</code>", html);
    }

    [Fact]
    public void Render_DocBlock_ListItem_ScriptStyle_InlineCodesEscaped()
    {
        var html = ParseAndRender(Markdown);

        // `<script>` and `<style>` inside inline code
        Assert.Contains("<code>&lt;script&gt;</code>", html);
        Assert.Contains("<code>&lt;style&gt;</code>", html);
    }

    [Fact]
    public void Render_DocBlock_NodeTypes_BoldRendered()
    {
        var html = ParseAndRender(Markdown);

        Assert.Contains("<strong>Node types</strong>", html);
    }

    [Fact]
    public void Render_DocBlock_FencedDiv_RendersOpeningTagWithLangAttribute()
    {
        var html = ParseAndRender(Markdown);

        Assert.Contains("<C:Codeblock lang=\"csharp\">", html);
        Assert.Contains("</C:Codeblock>", html);
    }

    [Fact]
    public void Render_DocBlock_FencedDiv_RawContentIsHTMLEscaped()
    {
        var html = ParseAndRender(Markdown);

        // The raw content contains "HTML elements (<div>, <p>, etc.)" — angle brackets must be escaped
        Assert.Contains("&lt;div&gt;", html);
        Assert.Contains("&lt;p&gt;", html);
    }

    [Fact]
    public void Render_DocBlock_FencedDiv_NodeTypeNamesPresent()
    {
        var html = ParseAndRender(Markdown);

        Assert.Contains("DocumentNode", html);
        Assert.Contains("ElementNode", html);
        Assert.Contains("TextNode", html);
        Assert.Contains("CommentNode", html);
        Assert.Contains("CDataNode", html);
        Assert.Contains("DocumentTypeNode", html);
        Assert.Contains("XmlElementNode", html);
    }

    [Fact]
    public void Render_DocBlock_UsageExample_RenderedAsBold()
    {
        var html = ParseAndRender(Markdown);

        Assert.Contains("<strong>Usage example</strong>", html);
    }

    [Fact]
    public void Render_DocBlock_FullOutput_ContainsAllMajorSections()
    {
        var html = ParseAndRender(Markdown);

        // Structural landmarks in order
        Assert.Contains("<h3>", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<C:Codeblock", html);
        Assert.Contains("</C:Codeblock>", html);

        // All eight list items present (by their bold labels)
        Assert.Contains("Elements with attributes", html);
        Assert.Contains("Self-closing tags", html);
        Assert.Contains("Void elements", html);
        Assert.Contains("Comments", html);
        Assert.Contains("CDATA sections", html);
        Assert.Contains("DOCTYPE declarations", html);
        Assert.Contains("SVG/MathML", html);
        Assert.Contains("Script/style preservation", html);
    }

    // -------------------------------------------------------------------------
    // Unicode and whitespace preservation in RawContent
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_DocBlock_FencedDiv_RawContent_PreservesUnicodeBoxDrawingChars()
    {
        // The codeblock content uses Unicode box-drawing chars (├─ └─) for the tree diagram.
        // The StreamReader-based pipeline must preserve these through UTF-8 decoding correctly.
        var doc = MarkdownParser.Parse(Markdown);
        var div = Assert.IsType<FencedDivNode>(doc.Children[5]);

        Assert.Contains("\u251c\u2500", div.RawContent); // ├─
        Assert.Contains("\u2514\u2500", div.RawContent); // └─
        Assert.DoesNotContain('\ufffd', div.RawContent); // no replacement chars (U+FFFD)
    }

    [Fact]
    public void Parse_DocBlock_FencedDiv_RawContent_PreservesInternalBlankLine()
    {
        // There is a blank line inside the codeblock between the ElementNode property
        // block and the TextNode line. It must be preserved in RawContent as \n\n.
        var doc = MarkdownParser.Parse(Markdown);
        var div = Assert.IsType<FencedDivNode>(doc.Children[5]);

        // The blank line separates "└─ IsVoidElement: bool" from "TextNode"
        Assert.Contains("IsVoidElement: bool\n\nTextNode", div.RawContent);
    }

    [Fact]
    public void Render_DocBlock_FencedDiv_RenderedHtml_PreservesInternalBlankLine()
    {
        // The blank line inside the codeblock must survive the render pipeline.
        var html = ParseAndRender(Markdown);

        // After HTML escaping \n is still \n — the blank line appears as two consecutive \n
        Assert.Contains("IsVoidElement: bool\n\nTextNode", html);
    }

    [Fact]
    public void Render_DocBlock_FencedDiv_RenderedHtml_PreservesUnicodeBoxDrawingChars()
    {
        // Box-drawing chars must survive the EscapeHtml pass (they are not special HTML chars).
        var html = ParseAndRender(Markdown);

        Assert.Contains("\u251c\u2500", html); // ├─
        Assert.Contains("\u2514\u2500", html); // └─
    }
}
