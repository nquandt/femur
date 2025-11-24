using System.Text;
using Femur.Markdown.Parser.Streaming;

namespace MarkdownRendererTests;

public class SpanParserTests
{
    [Fact]
    public void SimpleText()
    {
        var markdown = "Hello world";
        var output = new StringBuilder();

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        using var outputStream = new MemoryStream();
        using var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true);

        var renderer = new SpanMarkdownHtmlRenderer(writer);
        using var parser = new MarkdownStreamingParser<SpanMarkdownHtmlRenderer>(inputStream, renderer);

        parser.Parse();
        writer.Flush();

        outputStream.Position = 0;
        var result = new StreamReader(outputStream).ReadToEnd();

        Assert.Contains("Hello world", result);
    }

    [Fact]
    public void SimpleHeading()
    {
        var markdown = "# Title";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        using var outputStream = new MemoryStream();
        using var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true);

        var renderer = new SpanMarkdownHtmlRenderer(writer);
        using var parser = new MarkdownStreamingParser<SpanMarkdownHtmlRenderer>(inputStream, renderer);

        parser.Parse();
        writer.Flush();

        outputStream.Position = 0;
        var result = new StreamReader(outputStream).ReadToEnd();

        Assert.Contains("<h1>", result);
        Assert.Contains("Title", result);
        Assert.Contains("</h1>", result);
    }

    [Fact]
    public void SimpleList()
    {
        var markdown = @"- Item 1
- Item 2";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        using var outputStream = new MemoryStream();
        using var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true);

        var renderer = new SpanMarkdownHtmlRenderer(writer);
        using var parser = new MarkdownStreamingParser<SpanMarkdownHtmlRenderer>(inputStream, renderer);

        parser.Parse();
        writer.Flush();

        outputStream.Position = 0;
        var result = new StreamReader(outputStream).ReadToEnd();

        Assert.Contains("<ul>", result);
        Assert.Contains("Item 1", result);
        Assert.Contains("Item 2", result);
    }

    [Fact]
    public void ComplexDocument()
    {
        var markdown = @"# Title

This is a paragraph with **bold** and *italic* text.

## Subtitle

- First item
- Second item
- Third item

More text here.

1. Numbered one
2. Numbered two

> A quote

Final paragraph.";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        using var outputStream = new MemoryStream();
        using var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true);

        var renderer = new SpanMarkdownHtmlRenderer(writer);
        using var parser = new MarkdownStreamingParser<SpanMarkdownHtmlRenderer>(inputStream, renderer);

        parser.Parse();
        writer.Flush();

        outputStream.Position = 0;
        var result = new StreamReader(outputStream).ReadToEnd();

        Assert.Contains("<h1>", result);
        Assert.Contains("Title", result);
        Assert.Contains("<strong>", result);
        Assert.Contains("bold", result);
    }
}
