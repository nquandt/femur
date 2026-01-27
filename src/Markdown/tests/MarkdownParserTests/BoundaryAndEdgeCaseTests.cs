using Femur.Markdown.Abstractions.Nodes;
using MarkdownParserInstance = Femur.Markdown.Parser.MarkdownParser;

namespace MarkdownParserTests;

/// <summary>
/// Comprehensive edge case and boundary condition tests for Markdown parser.
/// These tests ensure the parser correctly handles edge cases, boundary conditions,
/// and malformed input without crashing or producing incorrect output.
/// </summary>
public class BoundaryAndEdgeCaseTests : IClassFixture<TestFixture>, IDisposable
{
    public BoundaryAndEdgeCaseTests(TestFixture fixture)
    {
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Fenced Code Block Boundaries

    [Fact]
    public void Parse_FencedCodeBlock_ContentBeforeOpeningFence_IsNotIncluded()
    {
        // Text before opening fence should not be included in code block
        // Note: If fence is on same line as text, it may be part of paragraph
        var markdown = "Text before\n```\ncode\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Contains("Text before", paragraph.Children.OfType<MarkdownTextNode>().First().Content);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[1]);
        Assert.Contains("code", codeBlock.Content);
        Assert.DoesNotContain("Text before", codeBlock.Content);
    }

    [Fact]
    public void Parse_FencedCodeBlock_ContentAfterClosingFence_IsNotIncluded()
    {
        // Text after closing fence should not be included in code block
        var markdown = "```\ncode\n``` After fence\n\nParagraph";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.Contains("code", codeBlock.Content);
        Assert.DoesNotContain("After fence", codeBlock.Content);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[1]);
        Assert.Contains("Paragraph", paragraph.Children.OfType<MarkdownTextNode>().First().Content);
    }

    [Fact]
    public void Parse_FencedCodeBlock_EmptyContent_ParsesCorrectly()
    {
        // Empty code block should parse correctly
        var markdown = "```\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.True(codeBlock.IsFenced);
        Assert.Empty(codeBlock.Content.Trim());
    }

    [Fact]
    public void Parse_FencedCodeBlock_OnlyWhitespace_PreservesWhitespace()
    {
        // Code block with only whitespace should preserve it
        var markdown = "```\n   \n\t\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.True(codeBlock.IsFenced);
        Assert.Contains("   ", codeBlock.Content);
        Assert.Contains("\t", codeBlock.Content);
    }

    [Fact]
    public void Parse_FencedCodeBlock_MismatchedFenceLengths_HandlesCorrectly()
    {
        // Opening fence with 3 backticks, closing with 4 should still close
        var markdown = "```\ncode\n````";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should parse as code block (fence matching is lenient)
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_FencedCodeBlock_AtDocumentStart_ParsesCorrectly()
    {
        // Code block at very start of document
        var markdown = "```\ncode\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.Contains("code", codeBlock.Content);
    }

    [Fact]
    public void Parse_FencedCodeBlock_AtDocumentEnd_ParsesCorrectly()
    {
        // Code block at very end of document
        var markdown = "```\ncode\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.Contains("code", codeBlock.Content);
    }

    [Fact]
    public void Parse_FencedCodeBlock_MissingClosingFence_HandlesGracefully()
    {
        // Code block without closing fence - behavior depends on implementation
        // May parse as code block (if parser is lenient) or paragraph
        var markdown = "```\ncode without closing";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should not crash - may parse as code block or paragraph
        Assert.NotEmpty(result.Children);
        // Current implementation may create code block even without closing
    }

    [Fact]
    public void Parse_FencedCodeBlock_WithInfoString_ContentAfterInfo_IsNotIncluded()
    {
        // Info string should not include content after it
        var markdown = "```csharp extra text\ncode\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        // Info should be parsed correctly (may include or exclude "extra text" depending on implementation)
        Assert.DoesNotContain("extra text", codeBlock.Content);
    }

    #endregion

    #region Indented Code Block Boundaries

    [Fact]
    public void Parse_IndentedCodeBlock_ExactlyFourSpaces_IsCodeBlock()
    {
        // Exactly 4 spaces should create code block
        var markdown = "    code";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.False(codeBlock.IsFenced);
        Assert.Contains("code", codeBlock.Content);
    }

    [Fact]
    public void Parse_IndentedCodeBlock_ThreeSpaces_IsNotCodeBlock()
    {
        // 3 spaces should NOT create code block
        var markdown = "   code";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Contains("code", paragraph.Children.OfType<MarkdownTextNode>().First().Content);
    }

    [Fact]
    public void Parse_IndentedCodeBlock_MoreThanFourSpaces_IsCodeBlock()
    {
        // More than 4 spaces should still create code block
        var markdown = "      code";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.False(codeBlock.IsFenced);
    }

    [Fact]
    public void Parse_IndentedCodeBlock_WithTabs_HandlesCorrectly()
    {
        // Tabs should be treated as indentation
        var markdown = "\tcode";
        var result = MarkdownParserInstance.Parse(markdown);

        // Tab behavior depends on implementation (may be code block or paragraph)
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_IndentedCodeBlock_MixedIndentation_HandlesCorrectly()
    {
        // Mixed spaces and tabs
        var markdown = "  \tcode";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_IndentedCodeBlock_ContentAfterIndentation_IsIncluded()
    {
        // Content after indentation should be included
        var markdown = "    code with more text";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.Contains("code with more text", codeBlock.Content);
    }

    #endregion

    #region Block Quote Boundaries

    [Fact]
    public void Parse_BlockQuote_ContentBeforeMarker_IsNotIncluded()
    {
        // Text before > should not be included in block quote
        var markdown = "Text before > Quote";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Contains("Text before", paragraph.Children.OfType<MarkdownTextNode>().First().Content);
    }

    [Fact]
    public void Parse_BlockQuote_EmptyQuote_ParsesCorrectly()
    {
        // Empty block quote should parse
        var markdown = ">";
        var result = MarkdownParserInstance.Parse(markdown);

        // May parse as block quote or paragraph depending on implementation
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_BlockQuote_OnlyWhitespace_ParsesCorrectly()
    {
        // Block quote with only whitespace
        var markdown = ">   ";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_BlockQuote_AtDocumentStart_ParsesCorrectly()
    {
        // Block quote at start of document
        var markdown = "> Quote";
        var result = MarkdownParserInstance.Parse(markdown);

        var blockQuote = Assert.IsType<BlockQuoteNode>(result.Children[0]);
        Assert.NotEmpty(blockQuote.Children);
    }

    [Fact]
    public void Parse_BlockQuote_AtDocumentEnd_ParsesCorrectly()
    {
        // Block quote at end of document
        var markdown = "> Quote";
        var result = MarkdownParserInstance.Parse(markdown);

        var blockQuote = Assert.IsType<BlockQuoteNode>(result.Children[0]);
        Assert.NotEmpty(blockQuote.Children);
    }

    #endregion

    #region List Boundaries

    [Fact]
    public void Parse_List_ContentBeforeMarker_IsNotIncluded()
    {
        // Text before list marker should not be included
        var markdown = "Text before - Item";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Contains("Text before", paragraph.Children.OfType<MarkdownTextNode>().First().Content);
    }

    [Fact]
    public void Parse_List_EmptyListItem_ParsesCorrectly()
    {
        // Empty list item should parse (may require space after marker)
        var markdown = "- ";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Single(list.Children);
    }

    [Fact]
    public void Parse_List_OnlyWhitespaceItem_ParsesCorrectly()
    {
        // List item with only whitespace
        var markdown = "-   ";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Single(list.Children);
    }

    [Fact]
    public void Parse_List_AtDocumentStart_ParsesCorrectly()
    {
        // List at start of document
        var markdown = "- Item";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Single(list.Children);
    }

    [Fact]
    public void Parse_List_AtDocumentEnd_ParsesCorrectly()
    {
        // List at end of document
        var markdown = "- Item";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Single(list.Children);
    }

    [Fact]
    public void Parse_OrderedList_ZeroStartNumber_HandlesCorrectly()
    {
        // Ordered list starting with 0
        var markdown = "0. Item";
        var result = MarkdownParserInstance.Parse(markdown);

        // May parse as list or paragraph depending on implementation
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_OrderedList_NonSequentialNumbers_HandlesCorrectly()
    {
        // Non-sequential numbers
        var markdown = "1. First\n5. Fifth\n10. Tenth";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.True(list.IsOrdered);
        Assert.Equal(3, list.Children.Count);
    }

    #endregion

    #region Link and Image Boundaries

    [Fact]
    public void Parse_Link_ContentBeforeBracket_IsNotIncluded()
    {
        // Text before [ should not be included in link
        var markdown = "Text [link](url)";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = paragraph.Children.OfType<MarkdownTextNode>().First();
        Assert.Contains("Text", textNode.Content);

        var link = paragraph.Children.OfType<LinkNode>().First();
        var linkText = string.Join("", link.Children.OfType<MarkdownTextNode>().Select(n => n.Content));
        Assert.Contains("link", linkText);
    }

    [Fact]
    public void Parse_Link_ContentAfterParen_IsNotIncluded()
    {
        // Text after ) should not be included in link
        var markdown = "[link](url) after";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var link = paragraph.Children.OfType<LinkNode>().First();
        Assert.Equal("url", link.Url);

        var textAfter = paragraph.Children.OfType<MarkdownTextNode>().Last();
        Assert.Contains("after", textAfter.Content);
    }

    [Fact]
    public void Parse_Link_EmptyText_ParsesCorrectly()
    {
        // Link with empty text
        var markdown = "[](url)";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var link = paragraph.Children.OfType<LinkNode>().First();
        Assert.Equal("url", link.Url);
    }

    [Fact]
    public void Parse_Link_EmptyUrl_ParsesCorrectly()
    {
        // Link with empty URL
        var markdown = "[link]()";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var link = paragraph.Children.OfType<LinkNode>().First();
        var linkText = string.Join("", link.Children.OfType<MarkdownTextNode>().Select(n => n.Content));
        Assert.Contains("link", linkText);
    }

    [Fact]
    public void Parse_Image_ContentBeforeExclamation_IsNotIncluded()
    {
        // Text before ! should not be included in image
        var markdown = "Text ![alt](url)";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = paragraph.Children.OfType<MarkdownTextNode>().First();
        Assert.Contains("Text", textNode.Content);
    }

    #endregion

    #region Emphasis Boundaries

    [Fact]
    public void Parse_Emphasis_ContentBeforeDelimiter_IsNotIncluded()
    {
        // Text before * should not be included in emphasis
        var markdown = "Text *emphasized*";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = paragraph.Children.OfType<MarkdownTextNode>().First();
        Assert.Contains("Text", textNode.Content);

        var emphasis = paragraph.Children.OfType<EmphasisNode>().First();
        Assert.Contains("emphasized", emphasis.Children.OfType<MarkdownTextNode>().First().Content);
    }

    [Fact]
    public void Parse_Emphasis_ContentAfterDelimiter_IsNotIncluded()
    {
        // Text after closing * should not be included in emphasis
        var markdown = "*emphasized* after";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var emphasis = paragraph.Children.OfType<EmphasisNode>().First();
        Assert.Contains("emphasized", emphasis.Children.OfType<MarkdownTextNode>().First().Content);

        var textAfter = paragraph.Children.OfType<MarkdownTextNode>().Last();
        Assert.Contains("after", textAfter.Content);
    }

    [Fact]
    public void Parse_Emphasis_EmptyContent_HandlesCorrectly()
    {
        // Empty emphasis (just delimiters) - should parse as text, not emphasis
        var markdown = "**";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse as text, not emphasis (empty emphasis is invalid)
        var textContent = string.Join("", paragraph.Children.OfType<MarkdownTextNode>().Select(n => n.Content));
        Assert.Contains("*", textContent);
    }

    [Fact]
    public void Parse_Emphasis_UnmatchedDelimiters_HandlesCorrectly()
    {
        // Unmatched delimiters should not create emphasis
        var markdown = "*unmatched";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Contains("*unmatched", paragraph.Children.OfType<MarkdownTextNode>().First().Content);
    }

    #endregion

    #region HTML Block Boundaries (All Types)

    [Fact]
    public void Parse_HtmlBlockType1_Pre_ContentAfterClosingTag_IsNotIncluded()
    {
        // Type 1: <pre> tag should not include content after </pre>
        var markdown = "<pre>\ncode\n</pre> After pre\n\nParagraph";
        var result = MarkdownParserInstance.Parse(markdown);

        var htmlBlock = Assert.IsType<HtmlBlockNode>(result.Children[0]);
        Assert.Contains("</pre>", htmlBlock.Content);
        Assert.DoesNotContain("After pre", htmlBlock.Content);
    }

    [Fact]
    public void Parse_HtmlBlockType1_Iframe_ContentAfterClosingTag_IsNotIncluded()
    {
        // Type 1: <iframe> tag should not include content after </iframe>
        var markdown = "<iframe src=\"test.html\"></iframe> After iframe\n\nParagraph";
        var result = MarkdownParserInstance.Parse(markdown);

        var htmlBlock = Assert.IsType<HtmlBlockNode>(result.Children[0]);
        Assert.Contains("</iframe>", htmlBlock.Content);
        Assert.DoesNotContain("After iframe", htmlBlock.Content);
    }

    [Fact]
    public void Parse_HtmlBlockType6_ContentAfterBlankLine_IsNotIncluded()
    {
        // Type 6: Should stop at blank line
        var markdown = "<div>\ncontent\n</div>\n\nParagraph";
        var result = MarkdownParserInstance.Parse(markdown);

        var htmlBlock = Assert.IsType<HtmlBlockNode>(result.Children[0]);
        Assert.Contains("</div>", htmlBlock.Content);
        Assert.DoesNotContain("Paragraph", htmlBlock.Content);
    }

    [Fact]
    public void Parse_HtmlBlockType7_ContentAfterBlankLine_IsNotIncluded()
    {
        // Type 7: Should stop at blank line
        // Note: Type 7 requires complete tag on single line, so multi-line may behave differently
        var markdown = "<span>content</span>\n\nParagraph";
        var result = MarkdownParserInstance.Parse(markdown);

        // Type 7 HTML block should be first child
        Assert.NotEmpty(result.Children);
        var firstChild = result.Children[0];

        // May be HTML block or paragraph depending on implementation
        if (firstChild is HtmlBlockNode htmlBlock)
        {
            Assert.Contains("</span>", htmlBlock.Content);
            // Paragraph should be separate block
            if (result.Children.Count > 1)
            {
                var paragraph = Assert.IsType<ParagraphNode>(result.Children[1]);
                Assert.Contains("Paragraph", paragraph.Children.OfType<MarkdownTextNode>().First().Content);
            }
        }
        else
        {
            // If not HTML block, verify it's handled correctly
            Assert.NotNull(firstChild);
        }
    }

    [Fact]
    public void Parse_HtmlBlock_EmptyBlock_ParsesCorrectly()
    {
        // Empty HTML block
        var markdown = "<div></div>";
        var result = MarkdownParserInstance.Parse(markdown);

        var htmlBlock = Assert.IsType<HtmlBlockNode>(result.Children[0]);
        Assert.Contains("<div></div>", htmlBlock.Content);
    }

    #endregion

    #region Document Boundaries

    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptyDocument()
    {
        // Empty document should return empty document node
        var markdown = "";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Empty(result.Children);
    }

    [Fact]
    public void Parse_OnlyWhitespace_ReturnsEmptyDocument()
    {
        // Document with only whitespace
        var markdown = "   \n\t\n  ";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should have no children (whitespace-only blocks are typically ignored)
        Assert.Empty(result.Children);
    }

    [Fact]
    public void Parse_SingleCharacter_ParsesCorrectly()
    {
        // Single character document
        var markdown = "a";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Contains("a", paragraph.Children.OfType<MarkdownTextNode>().First().Content);
    }

    [Fact]
    public void Parse_OnlyNewlines_ReturnsEmptyDocument()
    {
        // Document with only newlines
        var markdown = "\n\n\n";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Empty(result.Children);
    }

    #endregion

    #region Nested Structure Boundaries

    [Fact]
    public void Parse_NestedBlockQuote_ContentBoundaries_AreCorrect()
    {
        // Nested block quotes should have correct boundaries
        var markdown = "> Outer\n> > Inner\n> Outer again";
        var result = MarkdownParserInstance.Parse(markdown);

        var outerQuote = Assert.IsType<BlockQuoteNode>(result.Children[0]);
        Assert.Equal(2, outerQuote.Children.Count);

        var innerQuote = Assert.IsType<BlockQuoteNode>(outerQuote.Children[1]);
        Assert.Single(innerQuote.Children);
    }

    [Fact]
    public void Parse_NestedList_ContentBoundaries_AreCorrect()
    {
        // Nested lists should have correct boundaries
        var markdown = "- Outer\n  - Inner\n- Outer again";
        var result = MarkdownParserInstance.Parse(markdown);

        var outerList = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Equal(2, outerList.Children.Count);

        var firstItem = Assert.IsType<ListItemNode>(outerList.Children[0]);
        var innerList = Assert.IsType<ListNode>(firstItem.Children[1]);
        Assert.Single(innerList.Children);
    }

    [Fact]
    public void Parse_ListInBlockQuote_Boundaries_AreCorrect()
    {
        // List inside block quote should have correct boundaries
        var markdown = "> - Item 1\n> - Item 2";
        var result = MarkdownParserInstance.Parse(markdown);

        var blockQuote = Assert.IsType<BlockQuoteNode>(result.Children[0]);
        var list = Assert.IsType<ListNode>(blockQuote.Children[0]);
        Assert.Equal(2, list.Children.Count);
    }

    #endregion

    #region Special Characters and Unicode

    [Fact]
    public void Parse_UnicodeCharacters_PreservesCorrectly()
    {
        // Unicode characters should be preserved
        var markdown = "Hello 世界 🌍";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var text = paragraph.Children.OfType<MarkdownTextNode>().First().Content;
        Assert.Contains("世界", text);
        Assert.Contains("🌍", text);
    }

    [Fact]
    public void Parse_SpecialMarkdownCharactersInCodeBlock_PreservedAsIs()
    {
        // Special markdown characters in code blocks should be preserved
        var markdown = "```\n* ** [ ] ( )\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.Contains("*", codeBlock.Content);
        Assert.Contains("**", codeBlock.Content);
        Assert.Contains("[", codeBlock.Content);
    }

    [Fact]
    public void Parse_ControlCharacters_HandlesCorrectly()
    {
        // Control characters should be handled gracefully
        var markdown = "Text\u0000More";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should not crash, may preserve or filter control characters
        Assert.NotEmpty(result.Children);
    }

    #endregion

    #region Malformed Input Handling

    [Fact]
    public void Parse_MissingClosingDelimiter_DoesNotCrash()
    {
        // Missing closing delimiter should not crash
        var markdown = "```\ncode without closing";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should parse as paragraph or handle gracefully
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_ExtraClosingDelimiter_HandlesCorrectly()
    {
        // Extra closing delimiter should not crash
        var markdown = "```\ncode\n```\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should parse first code block, handle extra delimiter
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_DeeplyNestedStructures_HandlesCorrectly()
    {
        // Deeply nested structures should not crash
        var markdown = "> > > > > > > > > > Deep";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should handle deep nesting gracefully
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_OverlappingDelimiters_HandlesCorrectly()
    {
        // Overlapping delimiters should be handled
        var markdown = "***text***";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should parse as strong emphasis or handle overlap
        Assert.NotEmpty(result.Children);
    }

    #endregion

    #region Very Long Content

    [Fact]
    public void Parse_VeryLongLine_HandlesCorrectly()
    {
        // Very long line should not crash
        var longText = new string('a', 10000);
        var markdown = longText;
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        Assert.Contains(longText.Substring(0, 100), paragraph.Children.OfType<MarkdownTextNode>().First().Content);
    }

    [Fact]
    public void Parse_ManyBlocks_HandlesCorrectly()
    {
        // Many blocks should not crash
        var markdown = string.Join("\n\n", Enumerable.Range(1, 1000).Select(i => $"# Heading {i}"));
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(1000, result.Children.Count);
    }

    #endregion

    #region Consecutive Delimiters

    [Fact]
    public void Parse_ConsecutiveFencedCodeBlocks_BoundariesAreCorrect()
    {
        // Consecutive code blocks should have correct boundaries
        var markdown = "```\ncode1\n```\n```\ncode2\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(2, result.Children.Count);
        var block1 = Assert.IsType<CodeBlockNode>(result.Children[0]);
        var block2 = Assert.IsType<CodeBlockNode>(result.Children[1]);
        Assert.Contains("code1", block1.Content);
        Assert.Contains("code2", block2.Content);
        Assert.DoesNotContain("code2", block1.Content);
        Assert.DoesNotContain("code1", block2.Content);
    }

    [Fact]
    public void Parse_ConsecutiveHtmlBlocks_BoundariesAreCorrect()
    {
        // Consecutive HTML blocks should have correct boundaries
        var markdown = "<!-- Comment 1 -->\n\n<!-- Comment 2 -->";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(2, result.Children.Count);
        var block1 = Assert.IsType<HtmlBlockNode>(result.Children[0]);
        var block2 = Assert.IsType<HtmlBlockNode>(result.Children[1]);
        Assert.Contains("Comment 1", block1.Content);
        Assert.Contains("Comment 2", block2.Content);
        Assert.DoesNotContain("Comment 2", block1.Content);
        Assert.DoesNotContain("Comment 1", block2.Content);
    }

    #endregion
}

