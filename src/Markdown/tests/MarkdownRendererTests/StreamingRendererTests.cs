using System.Text;
using Femur.Markdown.Parser.Streaming;

namespace MarkdownRendererTests;

public class StreamingRendererTests
{
    [Fact]
    public void StreamingParser_SimpleHeading_RendersCorrectly()
    {
        var markdown = "# Hello World";
        var expected = "<h1>Hello World</h1>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_Paragraph_RendersCorrectly()
    {
        var markdown = "This is a paragraph.";
        var expected = "<p>This is a paragraph.</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_BoldAndItalic_RendersCorrectly()
    {
        var markdown = "Text with **bold** and *italic*.";
        var expected = "<p>Text with <strong>bold</strong> and <em>italic</em>.</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_CodeSpan_RendersCorrectly()
    {
        var markdown = "Inline `code` here.";
        var expected = "<p>Inline <code>code</code> here.</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_UnorderedList_RendersCorrectly()
    {
        var markdown = @"- Item 1
- Item 2
- Item 3";
        var expected = @"<ul>
<li>Item 1</li>
<li>Item 2</li>
<li>Item 3</li>
</ul>
";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_OrderedList_RendersCorrectly()
    {
        var markdown = @"1. First
2. Second
3. Third";
        var expected = @"<ol>
<li>First</li>
<li>Second</li>
<li>Third</li>
</ol>
";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_CodeBlock_RendersCorrectly()
    {
        var markdown = @"```csharp
public void Method() { }
```";
        var expected = "<pre><code class=\"language-csharp\">public void Method() { }</code></pre>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_Blockquote_RendersCorrectly()
    {
        var markdown = @"> This is a quote
> with two lines";
        var expected = @"<blockquote>
<p>This is a quote
with two lines</p>
</blockquote>
";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_Link_RendersCorrectly()
    {
        var markdown = "[Link text](https://example.com)";
        var expected = "<p><a href=\"https://example.com\">Link text</a></p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_Image_RendersCorrectly()
    {
        var markdown = "![Alt text](https://example.com/image.png)";
        var expected = "<p><img src=\"https://example.com/image.png\" alt=\"Alt text\" /></p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_ThematicBreak_RendersCorrectly()
    {
        var markdown = "---";
        var expected = "<hr />\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_ArabicText_RendersCorrectly()
    {
        var markdown = "الإستعداد للبدء!";
        var expected = "<p>الإستعداد للبدء!</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_ArabicHeading_RendersCorrectly()
    {
        var markdown = "# الإستعداد للبدء!";
        var expected = "<h1>الإستعداد للبدء!</h1>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_HeadingWithTrailingHashes_RendersCorrectly()
    {
        var markdown = "# Test heading #";
        var expected = "<h1>Test heading</h1>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_ArabicHeadingWithTrailingHashes_RendersCorrectly()
    {
        var markdown = "# الإستعداد للبدء! #";
        var expected = "<h1>الإستعداد للبدء!</h1>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_UnicodeEmoji_RendersCorrectly()
    {
        var markdown = "Hello 👋 World 🌍";
        // With optimized escaping, emojis pass through as UTF-8 (correct for modern HTML5)
        var expected = "<p>Hello 👋 World 🌍</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_ChineseText_RendersCorrectly()
    {
        var markdown = "你好世界";
        var expected = "<p>你好世界</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_MixedRTLAndLTR_RendersCorrectly()
    {
        var markdown = "Hello مرحبا World";
        var expected = "<p>Hello مرحبا World</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_ArabicWithEnglishInHeading_RendersCorrectly()
    {
        var markdown = "# Hello الإستعداد للبدء!";
        var expected = "<h1>Hello الإستعداد للبدء!</h1>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_SingleArabicCharacter_RendersCorrectly()
    {
        var markdown = "ا";
        var expected = "<p>ا</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_TwoArabicCharacters_RendersCorrectly()
    {
        var markdown = "اب";
        var expected = "<p>اب</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_ArabicWordAlone_RendersCorrectly()
    {
        var markdown = "مرحبا";
        var expected = "<p>مرحبا</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_ArabicWordInHeading_RendersCorrectly()
    {
        var markdown = "# مرحبا";
        var expected = "<h1>مرحبا</h1>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_ArabicWithExclamation_RendersCorrectly()
    {
        var markdown = "مرحبا!";
        var expected = "<p>مرحبا!</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_ExclamationWithArabic_RendersCorrectly()
    {
        var markdown = "!مرحبا";
        var expected = "<p>!مرحبا</p>\n";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_EmptyDocument_RendersCorrectly()
    {
        var markdown = "";
        var expected = "";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_OnlyWhitespace_RendersCorrectly()
    {
        var markdown = "   \n\n  \t\n";
        var expected = "";

        var result = RenderMarkdown(markdown);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void StreamingParser_LongLineOfText_RendersCorrectly()
    {
        var markdown = new string('a', 10000);
        var result = RenderMarkdown(markdown);

        Assert.StartsWith("<p>", result);
        Assert.EndsWith("</p>\n", result);
        Assert.Contains(new string('a', 1000), result);
    }

    private static string RenderMarkdown(string markdown)
    {
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        using var outputStream = new MemoryStream();
        using var writer = new StreamWriter(outputStream);
        using var renderer = new MarkdownHtmlStreamingRenderer(writer);
        using var parser = new MarkdownStreamingParser(inputStream, renderer);

        parser.Parse();
        writer.Flush();

        return Encoding.UTF8.GetString(outputStream.ToArray());
    }
}
