using Femur.Markdown.Abstractions;
using static Femur.Markdown.Abstractions.MarkdownNodeType;
using Femur.Markdown.Abstractions.Nodes;
using MarkdownParserInstance = Femur.Markdown.Parser.MarkdownParser;

namespace MarkdownParserTests;

/// <summary>
/// Tests for edge cases in list parsing, particularly around list item detection
/// and interaction with emphasis markers.
/// </summary>
public class ListEdgeCasesTests : IClassFixture<TestFixture>, IDisposable
{
    public ListEdgeCasesTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region List Item Detection with Asterisk

    [Fact]
    public void Parse_StrongEmphasisAfterList_NotTreatedAsListItem()
    {
        var markdown = @"- Item 1
- Item 2

**Bold text**";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(2, result.Children.Count);

        // First child should be a list with 2 items
        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.False(list.IsOrdered);
        Assert.Equal(2, list.Children.Count);

        // Second child should be a paragraph with strong emphasis, NOT a list item
        var paragraph = Assert.IsType<ParagraphNode>(result.Children[1]);
        Assert.Single(paragraph.Children);

        var strong = Assert.IsType<StrongEmphasisNode>(paragraph.Children[0]);
        Assert.Single(strong.Children);

        var text = Assert.IsType<MarkdownTextNode>(strong.Children[0]);
        Assert.Equal("Bold text", text.Content);
    }

    [Fact]
    public void Parse_EmphasisStartingWithAsterisk_NotTreatedAsListItem()
    {
        var markdown = @"Some paragraph.

*Italic text*

Another paragraph.";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(3, result.Children.Count);

        // All three should be paragraphs
        Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.IsType<ParagraphNode>(result.Children[1]);
        Assert.IsType<ParagraphNode>(result.Children[2]);

        // Second paragraph should contain emphasis
        var secondPara = (ParagraphNode)result.Children[1];
        Assert.Single(secondPara.Children);

        var emphasis = Assert.IsType<EmphasisNode>(secondPara.Children[0]);
        Assert.Single(emphasis.Children);

        var text = Assert.IsType<MarkdownTextNode>(emphasis.Children[0]);
        Assert.Equal("Italic text", text.Content);
    }

    [Fact]
    public void Parse_AsteriskWithoutWhitespace_NotTreatedAsListItem()
    {
        var markdown = @"- Item 1

**When to use which:**

Some content.";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(3, result.Children.Count);

        // First child should be a list with 1 item
        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Single(list.Children);

        // Second child should be a paragraph with strong emphasis
        var paragraph = Assert.IsType<ParagraphNode>(result.Children[1]);
        Assert.Single(paragraph.Children);

        var strong = Assert.IsType<StrongEmphasisNode>(paragraph.Children[0]);
        Assert.Single(strong.Children);

        var text = Assert.IsType<MarkdownTextNode>(strong.Children[0]);
        Assert.Equal("When to use which:", text.Content);

        // Third child should be a paragraph
        Assert.IsType<ParagraphNode>(result.Children[2]);
    }

    [Fact]
    public void Parse_MultipleAsterisksWithoutWhitespace_NotTreatedAsListItem()
    {
        var markdown = @"- Item 1
- Item 2

***Bold and italic***";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(2, result.Children.Count);

        // First child should be a list
        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Equal(2, list.Children.Count);

        // Second child should be a paragraph, not a list item
        var paragraph = Assert.IsType<ParagraphNode>(result.Children[1]);
        Assert.Single(paragraph.Children);

        // Should contain strong emphasis with nested emphasis
        var strong = Assert.IsType<StrongEmphasisNode>(paragraph.Children[0]);
        Assert.Single(strong.Children);

        var emphasis = Assert.IsType<EmphasisNode>(strong.Children[0]);
        Assert.Single(emphasis.Children);

        var text = Assert.IsType<MarkdownTextNode>(emphasis.Children[0]);
        Assert.Equal("Bold and italic", text.Content);
    }

    #endregion

    #region List Item Detection with Other Markers

    [Fact]
    public void Parse_HyphenWithoutWhitespace_NotTreatedAsListItem()
    {
        var markdown = @"- Item 1

--Not a list item--";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(2, result.Children.Count);

        // First child should be a list
        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Single(list.Children);

        // Second child should be a paragraph
        var paragraph = Assert.IsType<ParagraphNode>(result.Children[1]);
        Assert.Single(paragraph.Children);

        // Note: Smart punctuation converts -- to en dash (–)
        var text = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        Assert.Equal("–Not a list item–", text.Content);
    }

    [Fact]
    public void Parse_PlusWithoutWhitespace_NotTreatedAsListItem()
    {
        var markdown = @"+ Item 1

++Not a list item++";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(2, result.Children.Count);

        // First child should be a list
        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Single(list.Children);

        // Second child should be a paragraph
        var paragraph = Assert.IsType<ParagraphNode>(result.Children[1]);
        Assert.Single(paragraph.Children);

        var text = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        Assert.Equal("++Not a list item++", text.Content);
    }

    #endregion

    #region Valid List Items

    [Fact]
    public void Parse_AsteriskWithWhitespace_TreatedAsListItem()
    {
        var markdown = @"* Item 1
* Item 2";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.False(list.IsOrdered);
        Assert.Equal(2, list.Children.Count);
    }

    [Fact]
    public void Parse_HyphenWithWhitespace_TreatedAsListItem()
    {
        var markdown = @"- Item 1
- Item 2";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.False(list.IsOrdered);
        Assert.Equal(2, list.Children.Count);
    }

    [Fact]
    public void Parse_PlusWithWhitespace_TreatedAsListItem()
    {
        var markdown = @"+ Item 1
+ Item 2";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.False(list.IsOrdered);
        Assert.Equal(2, list.Children.Count);
    }

    #endregion

    #region Edge Cases - End of Line and Whitespace

    [Fact]
    public void Parse_MarkerAtEndOfLine_NotTreatedAsListItem()
    {
        var markdown = @"Some text.

*

More text.";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(3, result.Children.Count);

        // All three should be paragraphs, not lists
        Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.IsType<ParagraphNode>(result.Children[1]);
        Assert.IsType<ParagraphNode>(result.Children[2]);
    }

    [Fact]
    public void Parse_OrderedListMarkerWithoutWhitespace_NotTreatedAsListItem()
    {
        var markdown = @"1. Item 1

1.Not a list item";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(2, result.Children.Count);

        // First child should be a list
        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Single(list.Children);

        // Second child should be a paragraph
        var paragraph = Assert.IsType<ParagraphNode>(result.Children[1]);
        Assert.Single(paragraph.Children);
    }

    #endregion

    #region Tight vs Loose Lists

    [Fact]
    public void Parse_TightList_IsLooseFalse()
    {
        var markdown = @"- Item 1
- Item 2
- Item 3";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.False(list.IsLoose, "List without blank lines between items should be tight");
    }

    [Fact]
    public void Parse_LooseList_IsLooseTrue()
    {
        var markdown = @"- Item 1

- Item 2

- Item 3";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Single(result.Children);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.True(list.IsLoose, "List with blank lines between items should be loose");
    }

    [Fact]
    public void Parse_ListFollowedByNonListContent_RemainingTight()
    {
        var markdown = @"- Item 1
- Item 2

**Not a list item**";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(2, result.Children.Count);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.False(list.IsLoose, "List should be tight when non-list content follows with blank line");
        Assert.Equal(2, list.Children.Count);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void Parse_RealWorldExample_ListFollowedByBoldHeading()
    {
        var markdown = @"Why health checks work better:

- **Separation of concerns** - Config validation at startup. Health checks run continuously.
- **Orchestrator integration** - Kubernetes speaks health check natively.
- **Rich reporting** - Get detailed status info and custom metadata.
- **Non-blocking** - Failed health checks report degraded state. They don't prevent startup.
- **Ongoing monitoring** - Run on a schedule to catch issues that develop after deployment.

**When to use which:**

Startup validation for config that must be correct. Health checks for dependencies that might be temporarily down but shouldn't block startup. Think circuit breakers and retry policies.";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotNull(result);
        Assert.Equal(4, result.Children.Count);

        // First: "Why health checks work better:" paragraph
        var firstPara = Assert.IsType<ParagraphNode>(result.Children[0]);

        // Second: List with 5 items
        var list = Assert.IsType<ListNode>(result.Children[1]);
        Assert.Equal(5, list.Children.Count);

        // Third: Paragraph starting with "**When to use which:**" (NOT a list item)
        var thirdPara = Assert.IsType<ParagraphNode>(result.Children[2]);
        Assert.True(thirdPara.Children.Count > 0);

        var strong = Assert.IsType<StrongEmphasisNode>(thirdPara.Children[0]);
        Assert.Single(strong.Children);

        var text = Assert.IsType<MarkdownTextNode>(strong.Children[0]);
        Assert.Equal("When to use which:", text.Content);

        // Fourth: Paragraph starting with "Startup validation..."
        var fourthPara = Assert.IsType<ParagraphNode>(result.Children[3]);
    }

    #endregion
}
