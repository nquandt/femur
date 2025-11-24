using Femur.Markup.Abstractions.Nodes;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

public class StructureTests : IClassFixture<TestFixture>, IDisposable
{
    public StructureTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }
    #region Nested Elements

    [Fact]
    public void Parse_DeeplyNestedElements_ParsesCorrectly()
    {
        var html = "<div><div><div><div>Deep</div></div></div></div>";
        var result = HtmlParserInstance.Parse(html);

        var level1 = Assert.IsType<ElementNode>(result.Children[0]);
        var level2 = Assert.IsType<ElementNode>(level1.Children[0]);
        var level3 = Assert.IsType<ElementNode>(level2.Children[0]);
        var level4 = Assert.IsType<ElementNode>(level3.Children[0]);
        var text = Assert.IsType<TextNode>(level4.Children[0]);
        Assert.Equal("Deep", text.Content);
    }

    [Fact]
    public void Parse_MultipleSiblings_ParsesCorrectly()
    {
        var html = "<div>First</div><div>Second</div><div>Third</div>";
        var result = HtmlParserInstance.Parse(html);

        Assert.Equal(3, result.Children.Count);
        Assert.All(result.Children, child => Assert.IsType<ElementNode>(child));
    }

    #endregion

    #region Script and Style Tags

    [Fact]
    public void Parse_ScriptTag_PreservesContent()
    {
        var html = "<script>alert('Hello');</script>";
        var result = HtmlParserInstance.Parse(html);

        var script = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("script", script.TagName, ignoreCase: true);
        var text = Assert.IsType<TextNode>(script.Children[0]);
        Assert.Equal("alert('Hello');", text.Content);
    }

    [Fact]
    public void Parse_ScriptTagWithLessThan_PreservesAsText()
    {
        var html = "<script>if (x < 10) { }</script>";
        var result = HtmlParserInstance.Parse(html);

        var script = Assert.IsType<ElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(script.Children[0]);
        Assert.Contains("<", text.Content);
    }

    [Fact]
    public void Parse_StyleTag_PreservesContent()
    {
        var html = "<style>body { color: red; }</style>";
        var result = HtmlParserInstance.Parse(html);

        var style = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("style", style.TagName, ignoreCase: true);
        var text = Assert.IsType<TextNode>(style.Children[0]);
        Assert.Contains("color: red", text.Content);
    }

    [Fact]
    public void Parse_ScriptTagWithWhitespace_PreservesWhitespace()
    {
        var html = "<script>\n  var x = 1;\n</script>";
        var result = HtmlParserInstance.Parse(html);

        var script = Assert.IsType<ElementNode>(result.Children[0]);
        var text = Assert.IsType<TextNode>(script.Children[0]);
        Assert.Contains("\n", text.Content);
    }

    #endregion

    #region Malformed HTML Handling

    [Fact]
    public void Parse_MismatchedClosingTag_HandlesGracefully()
    {
        var html = "<div><p>Content</div></p>";
        var result = HtmlParserInstance.Parse(html);

        // Should still parse, matching tags as best as possible
        Assert.NotNull(result);
    }

    [Fact]
    public void Parse_UnclosedTag_ParsesWhatItCan()
    {
        var html = "<div><p>Content";
        var result = HtmlParserInstance.Parse(html);

        Assert.NotNull(result);
        var div = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.NotEmpty(div.Children);
    }

    [Fact]
    public void Parse_MultipleRootElements_ParsesAll()
    {
        var html = "<div>First</div><div>Second</div>";
        var result = HtmlParserInstance.Parse(html);

        Assert.Equal(2, result.Children.Count);
    }

    #endregion

    #region Void Elements

    [Fact]
    public void Parse_AllVoidElements_AreMarkedAsVoid()
    {
        var voidElements = new[] { "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr" };

        foreach (var tag in voidElements)
        {
            var html = $"<{tag}>";
            var result = HtmlParserInstance.Parse(html);

            var element = Assert.IsType<ElementNode>(result.Children[0]);
            Assert.True(element.IsVoidElement, $"Expected {tag} to be a void element");
        }
    }

    [Fact]
    public void Parse_VoidElementWithChildren_DoesNotParseChildren()
    {
        var html = "<br>This should not be a child</br>";
        var result = HtmlParserInstance.Parse(html);

        var br = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.True(br.IsVoidElement);
        // Void elements shouldn't have children, but text after them should be siblings
        if (result.Children.Count > 1)
        {
            var text = Assert.IsType<TextNode>(result.Children[1]);
            Assert.Contains("This should not be a child", text.Content);
        }
    }

    #endregion
}
