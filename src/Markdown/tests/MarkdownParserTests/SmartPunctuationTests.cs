using Femur.Markdown.Parser;
using Femur.Markdown.Abstractions.Nodes;

namespace MarkdownParserTests;

public class SmartPunctuationTests
{
    [Fact]
    public void Parse_SmartDoubleQuotes_TransformsCorrectly()
    {
        var markdown = "\"Hello,\" said the spider.";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        // Should transform "Hello," to "Hello,"
        Assert.Contains('\u201C', textNode.Content); // Left double quote
        Assert.Contains('\u201D', textNode.Content); // Right double quote
    }

    [Fact]
    public void Parse_SmartSingleQuotes_TransformsCorrectly()
    {
        var markdown = "'A', 'B', and 'C' are letters.";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        // Should transform single quotes to curly quotes
        Assert.Contains('\u2018', textNode.Content); // Left single quote
        Assert.Contains('\u2019', textNode.Content); // Right single quote
    }

    [Fact]
    public void Parse_Apostrophes_NotTransformedToQuotes()
    {
        var markdown = "Were you alive in the 70's?";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        // Should treat 's as apostrophe, not opening quote
        Assert.Contains('\u2019', textNode.Content); // Apostrophe
        Assert.DoesNotContain('\u2018', textNode.Content); // Should not have opening quote
    }

    [Fact]
    public void Parse_MixedApostrophesAndQuotes_HandlesCorrectly()
    {
        var markdown = "'We'll use Jane's boat and John's truck,' Jenna said.";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        // Should have opening quote, apostrophes, and closing quote
        Assert.Contains('\u2018', textNode.Content); // Opening quote
        Assert.Contains('\u2019', textNode.Content); // Apostrophes and closing quote
    }

    [Fact]
    public void Parse_EnDash_TwoHyphens()
    {
        var markdown = "en--en";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        Assert.Contains('\u2013', textNode.Content); // En-dash
    }

    [Fact]
    public void Parse_EmDash_ThreeHyphens()
    {
        var markdown = "em---em";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        Assert.Contains('\u2014', textNode.Content); // Em-dash
    }

    [Fact]
    public void Parse_MultipleDashes_ConvertsCorrectly()
    {
        var markdown = "four----";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        // 4 hyphens: Algorithm: 4/3 = 1 em-dash (3 hyphens), remainder 1
        // Since remainder is 1 and emDashes > 0, convert last em-dash to 2 en-dashes
        // Result: 2 en-dashes (homogeneous sequence preferred)
        Assert.Contains('\u2013', textNode.Content); // En-dash
        var enDashCount = textNode.Content.Count(c => c == '\u2013');
        Assert.Equal(2, enDashCount); // Should have exactly 2 en-dashes
        Assert.DoesNotContain('\u2014', textNode.Content); // No em-dash
    }

    [Fact]
    public void Parse_Ellipsis_ThreePeriods()
    {
        var markdown = "Ellipses...and...and....";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        Assert.Contains('\u2026', textNode.Content); // Ellipsis
    }

    [Fact]
    public void Parse_EscapedQuotes_RemainLiteral()
    {
        var markdown = "\\\"This is not smart.\\\"";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        // Escaped quotes should remain as straight quotes
        Assert.Contains('"', textNode.Content);
        Assert.DoesNotContain('\u201C', textNode.Content);
        Assert.DoesNotContain('\u201D', textNode.Content);
    }

    [Fact]
    public void Parse_EscapedDashes_RemainLiteral()
    {
        var markdown = "Escaped hyphens: \\-- \\-\\-\\-.";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        // Escaped dashes should remain as hyphens
        Assert.Contains("--", textNode.Content);
        Assert.Contains("---", textNode.Content);
    }

    [Fact]
    public void Parse_EscapedEllipsis_RemainsLiteral()
    {
        var markdown = "No ellipses\\.\\.\\.";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        // Escaped periods should remain as periods
        Assert.Contains("...", textNode.Content);
        Assert.DoesNotContain('\u2026', textNode.Content);
    }

    [Fact]
    public void Parse_UnmatchedDoubleQuote_TreatedAsOpening()
    {
        var markdown = "\"A paragraph with no closing quote.";
        var result = MarkdownParser.Parse(markdown);

        var paragraph = Assert.IsType<ParagraphNode>(result.Children[0]);
        var textNode = Assert.IsType<MarkdownTextNode>(paragraph.Children[0]);
        // Unmatched quote should be treated as opening quote
        Assert.Contains('\u201C', textNode.Content); // Left double quote
        Assert.DoesNotContain('\u201D', textNode.Content); // No right quote
    }
}

