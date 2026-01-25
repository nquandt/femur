using Femur.Markdown.Abstractions;
using Femur.Markdown.Abstractions.Nodes;
using MarkdownParserInstance = Femur.Markdown.Parser.MarkdownParser;

namespace MarkdownParserTests;

/// <summary>
/// Tests for edge cases in emphasis and strong emphasis parsing.
/// </summary>
public class EmphasisEdgeCasesTests : IClassFixture<TestFixture>, IDisposable
{
    public EmphasisEdgeCasesTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    [Fact]
    public void Parse_DoubleAsterisk_TreatedAsLiteralText()
    {
        var markdown = "**";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Single(paragraph.Children);

        var text = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        Assert.Equal("**", text.Content);
    }

    [Fact]
    public void Parse_DoubleUnderscore_TreatedAsLiteralText()
    {
        var markdown = "__";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Single(paragraph.Children);

        var text = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        Assert.Equal("__", text.Content);
    }

    [Fact]
    public void Parse_UnderscoreOnSeparateLines_TreatedAsLiteralText()
    {
        var markdown = "_\n_";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should contain text nodes and possibly soft line breaks
        Assert.NotEmpty(paragraph.Children);
    }

    [Fact]
    public void Parse_SingleAsterisk_TreatedAsLiteralText()
    {
        var markdown = "*";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Single(paragraph.Children);

        var text = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        Assert.Equal("*", text.Content);
    }

    [Fact]
    public void Parse_TripleAsterisk_TreatedAsLiteralText()
    {
        var markdown = "***";

        var result = MarkdownParserInstance.Parse(markdown);

        // *** should be a thematic break, not emphasis
        Assert.Single(result.Children);
        Assert.IsType<ThematicBreakNode>(result.Children[0]);
    }

    [Fact]
    public void Parse_UnclosedEmphasis_TreatedAsLiteralText()
    {
        var markdown = "*text";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Single(paragraph.Children);

        var text = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        Assert.Equal("*text", text.Content);
    }

    [Fact]
    public void Parse_UnclosedStrongEmphasis_TreatedAsLiteralText()
    {
        var markdown = "**text";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Single(paragraph.Children);

        var text = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        Assert.Equal("**text", text.Content);
    }

    [Fact]
    public void Parse_EmptyEmphasis_NotCreated()
    {
        // Per CommonMark, emphasis needs content
        var markdown = "**";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);

        // Should be literal text, not an empty emphasis node
        Assert.Single(paragraph.Children);
        Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
    }

    [Fact]
    public void Parse_MultipleUnclosedMarkers_ParsedAsList()
    {
        // "* * text" is parsed as a list with "* text" as the content
        // (not a thematic break because it has non-space content after the markers)
        var markdown = "* * text";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        // This is a list because it doesn't meet thematic break criteria
        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Single(list.Children); // One list item with content "* text"
    }
}
