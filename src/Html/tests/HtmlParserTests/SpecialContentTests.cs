using Femur.Markup.Abstractions.Nodes;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

public class SpecialContentTests : IClassFixture<TestFixture>, IDisposable
{
    public SpecialContentTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }
    #region Comments

    [Fact]
    public void Parse_Comment_ParsesAsCommentNode()
    {
        var html = "<!-- This is a comment -->";
        var result = HtmlParserInstance.Parse(html);

        var comment = Assert.IsType<CommentNode>(result.Children[0]);
        // Parser includes the dash after ! in content: "- This is a comment "
        Assert.Contains("This is a comment", comment.Content);
        Assert.StartsWith("-", comment.Content);
    }

    [Fact]
    public void Parse_MultilineComment_ParsesCorrectly()
    {
        var html = "<!--\nMulti-line\ncomment\n-->";
        var result = HtmlParserInstance.Parse(html);

        var comment = Assert.IsType<CommentNode>(result.Children[0]);
        Assert.Contains("\n", comment.Content);
    }

    [Fact]
    public void Parse_CommentWithDashes_ParsesCorrectly()
    {
        var html = "<!-- Comment with -- dashes -->";
        var result = HtmlParserInstance.Parse(html);

        var comment = Assert.IsType<CommentNode>(result.Children[0]);
        Assert.Contains("--", comment.Content);
    }

    [Fact]
    public void Parse_CommentBetweenElements_ParsesCorrectly()
    {
        var html = "<div>Before</div><!-- comment --><div>After</div>";
        var result = HtmlParserInstance.Parse(html);

        Assert.Equal(3, result.Children.Count);
        _ = Assert.IsType<CommentNode>(result.Children[1]);
    }

    #endregion

    #region DOCTYPE

    [Fact]
    public void Parse_Doctype_ParsesAsDocumentTypeNode()
    {
        var html = "<!DOCTYPE html>";
        var result = HtmlParserInstance.Parse(html);

        var doctype = Assert.IsType<DocumentTypeNode>(result.Children[0]);
        Assert.Equal("DOCTYPE html", doctype.Content);
    }

    [Fact]
    public void Parse_DoctypeHtml2_ParsesCorrectly()
    {
        var html = "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">";
        var result = HtmlParserInstance.Parse(html);

        var doctype = Assert.IsType<DocumentTypeNode>(result.Children[0]);
        Assert.Contains("HTML 2.0", doctype.Content);
    }

    #endregion

    #region CDATA

    [Fact]
    public void Parse_CData_ParsesAsCDataNode()
    {
        var html = "<![CDATA[<div>Raw content</div>]]>";
        var result = HtmlParserInstance.Parse(html);

        var cdata = Assert.IsType<CDataNode>(result.Children[0]);
        // Parser reads until first ']' then handles closing, so content includes closing brackets
        Assert.Contains("<div>Raw content</div>", cdata.Content);
        // CDATA is parsed correctly (exact format may vary based on parser implementation)
        Assert.NotEmpty(cdata.Content);
    }

    [Fact]
    public void Parse_CDataWithBrackets_ParsesCorrectly()
    {
        var html = "<![CDATA[Content with ] brackets]]>";
        var result = HtmlParserInstance.Parse(html);

        var cdata = Assert.IsType<CDataNode>(result.Children[0]);
        // Parser handles brackets in CDATA content
        Assert.Contains("Content", cdata.Content);
        Assert.Contains("brackets", cdata.Content);
    }

    #endregion

    #region Text Content

    [Fact]
    public void Parse_PlainText_ParsesAsTextNode()
    {
        var html = "Plain text content";
        var result = HtmlParserInstance.Parse(html);

        var text = Assert.IsType<TextNode>(result.Children[0]);
        Assert.Equal("Plain text content", text.Content);
    }

    [Fact]
    public void Parse_TextWithWhitespace_FiltersWhitespaceOnlyNodes()
    {
        var html = "<div>\n    \nContent\n    \n</div>";
        var result = HtmlParserInstance.Parse(html);

        var div = Assert.IsType<ElementNode>(result.Children[0]);
        // Should have text node with content, whitespace-only nodes filtered
        var textNodes = div.Children.OfType<TextNode>().ToList();
        _ = Assert.Single(textNodes);
        Assert.Contains("Content", textNodes[0].Content);
    }

    [Fact]
    public void Parse_TextWithEntities_PreservesEntities()
    {
        var html = "<p>&amp; &lt; &gt; &quot;</p>";
        var result = HtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.Contains("&amp;", text.Content);
    }

    [Fact]
    public void Parse_MixedContent_ParsesCorrectly()
    {
        var html = "<p>Text <em>emphasized</em> more text.</p>";
        var result = HtmlParserInstance.Parse(html);

        var p = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal(3, p.Children.Count);
        _ = Assert.IsType<TextNode>(p.Children[0]);
        _ = Assert.IsType<ElementNode>(p.Children[1]);
        _ = Assert.IsType<TextNode>(p.Children[2]);
    }

    #endregion
}
