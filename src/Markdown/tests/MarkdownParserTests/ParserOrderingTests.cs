using Femur.Markdown.Abstractions;
using static Femur.Markdown.Abstractions.MarkdownNodeType;
using Femur.Markdown.Abstractions.Nodes;
using MarkdownParserInstance = Femur.Markdown.Parser.MarkdownParser;

namespace MarkdownParserTests;

/// <summary>
/// Tests to verify and document the parser's block-level construct ordering.
/// The order matters because some markers can be ambiguous.
///
/// Parser Priority Order (from highest to lowest):
/// 1. ATX Headings (#, ##, etc.)
/// 2. Fenced Divs (:::)
/// 3. Fenced Code Blocks (```, ~~~)
/// 4. Block Quotes (>)
/// 5. Lists (-, *, +, 1.)
/// 6. Link Reference Definitions
/// 7. HTML Blocks
/// 8. Thematic Breaks (---, ***, ___)
/// 9. Paragraphs (with Setext heading lookahead)
///
/// This ordering is critical for correct parsing of ambiguous constructs.
/// </summary>
public class ParserOrderingTests : IClassFixture<TestFixture>, IDisposable
{
    public ParserOrderingTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region ATX Headings Priority

    [Fact]
    public void Parse_AtxHeading_TakesPriorityOverThematicBreak()
    {
        // ### could be interpreted as a thematic break if not checked first
        var markdown = "### Heading";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        var heading = Assert.IsType<HeadingNode>(result.Children[0]);
        Assert.Equal(3, heading.Level);
    }

    #endregion

    #region Block Quotes Priority

    [Fact]
    public void Parse_BlockQuote_TakesPriorityOverList()
    {
        // > could be confused with other constructs
        var markdown = "> Quote\n> More quote";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        Assert.IsType<BlockQuoteNode>(result.Children[0]);
    }

    #endregion

    #region Lists vs Thematic Breaks

    [Fact]
    public void Parse_ListMarker_TakesPriorityOverThematicBreak()
    {
        // Single * followed by space is a list, not a thematic break (which needs 3+)
        var markdown = "* List item";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.False(list.IsOrdered);
    }

    [Fact]
    public void Parse_ThematicBreak_NotConfusedWithList()
    {
        // *** without content is a thematic break, not a list
        var markdown = "***";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        Assert.IsType<ThematicBreakNode>(result.Children[0]);
    }

    [Fact]
    public void Parse_ThematicBreakWithSpaces_NotConfusedWithList()
    {
        // * * * should be a thematic break per CommonMark
        var markdown = "* * *";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        Assert.IsType<ThematicBreakNode>(result.Children[0]);
    }

    #endregion

    #region Thematic Breaks vs Setext Headings

    [Fact]
    public void Parse_SetextHeading_TakesPriorityOverThematicBreak()
    {
        // --- after a paragraph is a Setext underline, not a thematic break
        var markdown = @"Heading text
---";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        var heading = Assert.IsType<HeadingNode>(result.Children[0]);
        Assert.Equal(2, heading.Level);
    }

    [Fact]
    public void Parse_ThematicBreakAlone_NotSetextHeading()
    {
        // --- without preceding paragraph is a thematic break
        var markdown = "---";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        Assert.IsType<ThematicBreakNode>(result.Children[0]);
    }

    [Fact]
    public void Parse_ThematicBreakAfterBlankLine_NotSetextHeading()
    {
        // --- after blank line is a thematic break, not a Setext underline
        var markdown = @"Paragraph text

---";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(2, result.Children.Count);
        Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.IsType<ThematicBreakNode>(result.Children[1]);
    }

    #endregion

    #region Fenced Code Blocks Priority

    [Fact]
    public void Parse_FencedCodeBlock_TakesPriorityOverList()
    {
        // ``` at start of line is code block, not a paragraph starting with backticks
        var markdown = @"```
code
```";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        Assert.IsType<CodeBlockNode>(result.Children[0]);
    }

    #endregion

    #region Complex Ordering Scenarios

    [Fact]
    public void Parse_MultipleAmbiguousConstructs_ParsedInCorrectOrder()
    {
        var markdown = @"# Heading

> Quote
> More quote

- List item
- Another item

---

Paragraph text
===

```
code
```

***

Final paragraph";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(8, result.Children.Count);
        Assert.IsType<HeadingNode>(result.Children[0]);      // ATX heading
        Assert.IsType<BlockQuoteNode>(result.Children[1]);   // Block quote
        Assert.IsType<ListNode>(result.Children[2]);         // List
        Assert.IsType<ThematicBreakNode>(result.Children[3]); // Thematic break
        Assert.IsType<HeadingNode>(result.Children[4]);      // Setext heading
        Assert.IsType<CodeBlockNode>(result.Children[5]);    // Code block
        Assert.IsType<ThematicBreakNode>(result.Children[6]); // Thematic break
        Assert.IsType<ParagraphNode>(result.Children[7]);    // Final paragraph
    }

    [Fact]
    public void Parse_ListFollowedByThematicBreak_BothParsedCorrectly()
    {
        var markdown = @"- Item 1
- Item 2

---

Next paragraph";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(3, result.Children.Count);
        Assert.IsType<ListNode>(result.Children[0]);
        Assert.IsType<ThematicBreakNode>(result.Children[1]);
        Assert.IsType<ParagraphNode>(result.Children[2]);
    }

    [Fact]
    public void Parse_AmbiguousHyphenUsage_ParsedCorrectly()
    {
        // Test all the different meanings of "-"
        var markdown = @"- List item

---

Heading
---

- Another list";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(4, result.Children.Count);
        Assert.IsType<ListNode>(result.Children[0]);         // List
        Assert.IsType<ThematicBreakNode>(result.Children[1]); // Thematic break
        Assert.IsType<HeadingNode>(result.Children[2]);      // Setext heading
        Assert.IsType<ListNode>(result.Children[3]);         // List
    }

    [Fact]
    public void Parse_AmbiguousAsteriskUsage_ParsedCorrectly()
    {
        // Test all the different meanings of "*"
        var markdown = @"* List item

***

* Another list";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(3, result.Children.Count);
        Assert.IsType<ListNode>(result.Children[0]);         // List
        Assert.IsType<ThematicBreakNode>(result.Children[1]); // Thematic break
        Assert.IsType<ListNode>(result.Children[2]);         // List
    }

    #endregion

    #region Edge Cases in Ordering

    [Fact]
    public void Parse_SetextUnderlineInCodeBlock_NotParsedAsHeading()
    {
        var markdown = @"```
Some text
---
```";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        // Content should include the --- as literal text
        Assert.Contains("---", codeBlock.Content);
    }

    [Fact]
    public void Parse_ListInBlockQuote_NestedCorrectly()
    {
        var markdown = @"> - Item 1
> - Item 2";

        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Single(result.Children);
        var blockQuote = Assert.IsType<BlockQuoteNode>(result.Children[0]);
        Assert.Single(blockQuote.Children);
        Assert.IsType<ListNode>(blockQuote.Children[0]);
    }

    #endregion
}
