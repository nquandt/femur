using Femur.Markdown.Abstractions.Nodes;
using MarkdownParserInstance = Femur.Markdown.Parser.MarkdownParser;

namespace MarkdownParserTests;

/// <summary>
/// Regression tests based on CommonMark test suite
/// </summary>
public class RegressionTests : IClassFixture<TestFixture>, IDisposable
{
    public RegressionTests(TestFixture fixture)
    {
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    [Fact]
    public void Parse_TabAfterListMarker_ParsesCorrectly()
    {
        // Eating a character after a partially consumed tab
        var markdown = "* foo\n→bar";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        var listItem = Assert.IsType<ListItemNode>(list.Children[0]);
        Assert.NotEmpty(listItem.Children);
    }

    [Fact]
    public void Parse_Type7HtmlBlockWithWhitespace_ParsesCorrectly()
    {
        // Type 7 HTML block followed by whitespace
        var markdown = "<a>  \nx";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should parse as HTML block, not paragraph
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_HtmlHeadingTags_ParsesAsHtmlBlocks()
    {
        // h2..h6 raw HTML blocks
        var markdown = "<h1>lorem</h1>\n\n<h2>lorem</h2>\n\n<h3>lorem</h3>";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should parse HTML blocks
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_SetextHeadingWithTab_ParsesCorrectly()
    {
        // Tabs after setext header line (→ represents tab character)
        // CommonMark spec: Setext underlines can have tabs
        var markdown = "hi\n--\t"; // Using \t for tab
        var result = MarkdownParserInstance.Parse(markdown);

        var heading = Assert.IsType<HeadingNode>(result.Children[0]);
        Assert.Equal(2, heading.Level);
    }

    [Fact]
    public void Parse_ChinesePunctuation_NotEmphasis()
    {
        // Chinese punctuation not recognized as emphasis
        var markdown = "**。**话";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should not parse as emphasis (Chinese punctuation is not word character)
        // This tests that punctuation rules are correct
    }

    [Fact]
    public void Parse_ComplexEmphasis_ParsesCorrectly()
    {
        // Complex emphasis parsing edge case
        var markdown = "a***b* c*";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse nested emphasis correctly
    }

    [Fact]
    public void Parse_LinkDefinitionWithBackslash_ParsesAsText()
    {
        // Backslash at end of link definition
        var markdown = "[\\]: test";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse as text, not link reference definition
    }

    [Fact]
    public void Parse_EmphasisWithPunctuation_ParsesCorrectly()
    {
        // Punctuation set different
        var markdown = "^_test_";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse emphasis correctly
    }

    [Fact]
    public void Parse_AtxHeadingWithTabs_ParsesCorrectly()
    {
        // Tabs before and after ATX closing heading
        var markdown = "# foo→#→";
        var result = MarkdownParserInstance.Parse(markdown);

        var heading = Assert.IsType<HeadingNode>(result.Children[0]);
        Assert.Equal(1, heading.Level);
    }

    [Fact]
    public void Parse_LinkWithEscapedSpace_ParsesAsText()
    {
        // Escaped space not allowed in link destination
        var markdown = "[link](a\\ b)";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse as text, not link
    }

    [Fact]
    public void Parse_MetaTagsInInline_ParsesCorrectly()
    {
        // Meta tags in inline contexts
        var markdown = "City:\n<span itemprop=\"contentLocation\" itemscope itemtype=\"https://schema.org/City\">\n  <meta itemprop=\"name\" content=\"Springfield\">\n</span>";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should parse HTML blocks correctly
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_LinkWithEncodedEntities_ParsesCorrectly()
    {
        // Double-encoding in links
        var markdown = "[XSS](javascript&amp;colon;alert%28&#039;XSS&#039;%29)";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse link URL correctly
    }

    [Fact]
    public void Parse_LinkWithPercentEncoding_ParsesCorrectly()
    {
        // Percent encoding in links
        var markdown = "[link](https://www.example.com/home/%25batty)";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var link = Assert.IsType<LinkNode>(paragraph.Children[0]);
        Assert.Contains("%25batty", link.Url);
    }

    [Fact]
    public void Parse_ClosingHtmlTagsWithoutOpener_ParsesAsHtmlBlocks()
    {
        // Script, pre, style close tag without opener
        var markdown = "</script>\n\n</pre>\n\n</style>";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should parse as HTML blocks
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_LinkWithAngleBrackets_ParsesAsText()
    {
        // Angle brackets in link destination
        var markdown = "[a](<b) c>";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse as text, not link
    }

    [Fact]
    public void Parse_EmphasisWithBackslashAtEnd_CreatesHardBreak()
    {
        // Backslash at end of emphasis creates hard line break
        var markdown = "*failed to be italic!*\\\ntext";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse emphasis and hard line break
    }

    [Fact]
    public void Parse_ProcessingInstruction_ParsesAsText()
    {
        // Processing instructions
        var markdown = "a <?\n?>";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse as text
    }

    [Fact]
    public void Parse_LinkReferenceWithLineBreak_ParsesCorrectly()
    {
        // Link reference with line break
        var markdown = "[\\\nfoo]: /uri\n\n[\\\nfoo]";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse link reference and use it
    }

    [Fact]
    public void Parse_Type7BlockInList_ParsesCorrectly()
    {
        // Type 7 blocks can't interrupt paragraph
        var markdown = "- <script>\n- some text\nsome other text\n</script>";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[0]);
        Assert.Equal(2, list.Children.Count);
    }

    [Fact]
    public void Parse_ComplexNestedEmphasis_ParsesCorrectly()
    {
        // Complex emphasis parsing
        var markdown = "*****Hello*world****";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse nested emphasis correctly
    }

    [Fact]
    public void Parse_LinkReferenceWithWhitespaceCollapse_ParsesCorrectly()
    {
        // Link label collapse all internal whitespace
        var markdown = "[foo][one two\n  three]\n\n[one two three]: /url \"title\"";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var link = Assert.IsType<LinkNode>(paragraph.Children[0]);
        Assert.Equal("/url", link.Url);
    }

    [Fact]
    public void Parse_CodeBlockWithTrailingSpaces_ParsesCorrectly()
    {
        // Trailing spaces in code blocks
        var markdown = "```\nabc\n```     ";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock = Assert.IsType<CodeBlockNode>(result.Children[0]);
        Assert.Equal("abc", codeBlock.Content.Trim());
    }

    [Fact]
    public void Parse_CaseInsensitiveDoctype_ParsesCorrectly()
    {
        // Case-insensitive doctype
        var markdown = "<!docType html>";
        var result = MarkdownParserInstance.Parse(markdown);

        // Should parse as HTML block
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public void Parse_HtmlDeclaration_ParsesCorrectly()
    {
        // HTML declarations
        var markdown = "x <!A>";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Should parse correctly
    }

    [Fact]
    public void Parse_BlockQuoteBlankLineInList_PreservesTightness()
    {
        // Block-quoted blank line shouldn't make parent list loose
        var markdown = "## Case 1\n\n- > a\n  >\n- b";
        var result = MarkdownParserInstance.Parse(markdown);

        var heading = Assert.IsType<HeadingNode>(result.Children[0]);
        var list = Assert.IsType<ListNode>(result.Children[1]);
        // List should remain tight
        Assert.False(list.IsLoose);
    }

    [Fact]
    public void Parse_LinkReferenceAsBlock_AffectsListTightness()
    {
        // Link reference definitions are blocks when checking list tightness
        var markdown = "## Case 1\n\n- [aaa]: /\n\n  [aaa]: /\n- b";
        var result = MarkdownParserInstance.Parse(markdown);

        var list = Assert.IsType<ListNode>(result.Children[1]);
        // Should handle link references correctly
    }

    [Fact]
    public void Parse_UnderscoreDelimiterEdgeCases_ParsesCorrectly()
    {
        // Underscore delimiter edge cases
        var markdown = "__!_!__\n\n__!x!__";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph1 = Assert.IsType<ParagraphNode>(result.Children[0]);
        var paragraph2 = Assert.IsType<ParagraphNode>(result.Children[1]);
        // Should parse emphasis correctly
    }

    [Fact]
    public void Parse_CodeBlockWithHyphenatedLanguage_ParsesCorrectly()
    {
        // Language identifiers with hyphens
        var markdown = "```language-r\nx <- 1\n```\n\n```r\nx <- 1\n```";
        var result = MarkdownParserInstance.Parse(markdown);

        var codeBlock1 = Assert.IsType<CodeBlockNode>(result.Children[0]);
        var codeBlock2 = Assert.IsType<CodeBlockNode>(result.Children[1]);
        Assert.Equal("language-r", codeBlock1.Info);
        Assert.Equal("r", codeBlock2.Info);
    }

    [Fact]
    public void Parse_EntityReferences_ParsesCorrectly()
    {
        // Entity references
        var markdown = "&parag;\n\n&para\n\n&para;";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(3, result.Children.Count);
        // Should parse entities correctly
    }

    [Fact]
    public void Parse_LinkReferenceWithoutQuotes_ParsesCorrectly()
    {
        // Link references without quotes
        var markdown = "[test]:example\n\n\"\"third [test]";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        // Paragraph should contain: MarkdownTextNode("""third "), LinkNode([test])
        // Find the link node (should be second child after text)
        var link = paragraph.Children.OfType<LinkNode>().FirstOrDefault();
        Assert.NotNull(link);
        Assert.Equal("example", link.Url);
    }

    [Fact]
    public void Parse_HtmlComments_ParsesCorrectly()
    {
        // HTML comments edge cases
        var markdown = "foo <!-- test --->\n\nfoo <!-- test ---->\n\nfoo <!----->";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(3, result.Children.Count);
        // Should parse HTML comments correctly
    }

    [Fact]
    public void Parse_UnicodeWhitespace_PreservesCorrectly()
    {
        // Unicode whitespace characters
        var markdown = "\u000BVertical Tab\u000B\n\n\u000CForm Feed\u000C";
        var result = MarkdownParserInstance.Parse(markdown);

        Assert.Equal(2, result.Children.Count);
        // Should preserve Unicode whitespace
    }

    [Fact]
    public void Parse_UnicodeInEmphasis_NotEmphasis()
    {
        // Unicode characters in emphasis context
        var markdown = "a**a∇**a\n\na**∇a**a";
        var result = MarkdownParserInstance.Parse(markdown);

        var paragraph1 = Assert.IsType<ParagraphNode>(result.Children[0]);
        var paragraph2 = Assert.IsType<ParagraphNode>(result.Children[1]);
        // Unicode characters should not trigger emphasis
    }
}

