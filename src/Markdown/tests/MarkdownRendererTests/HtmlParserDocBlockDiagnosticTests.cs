using Femur.Markdown.Abstractions.Nodes;
using Femur.Markdown.Parser;
using Femur.Markdown.Renderer;
using Femur.Parsing.Nodes;

namespace MarkdownRendererTests;

/// <summary>
/// Diagnostic test — dumps the full AST and rendered HTML for the doc block
/// to stdout so we can inspect the actual parser/renderer output for bugs.
/// Not part of the regular test suite (uses output helpers only).
/// </summary>
public class HtmlParserDocBlockDiagnosticTests
{
    private readonly MarkdownHtmlRenderer _renderer = new();

    private const string Markdown = """
### HTML Parser: Standard Markup Parsing

The `HtmlParser` provides streaming HTML 2.0 parsing with AST generation:

**Key features**:
- **Elements with attributes** - case-preserved tag names, lazy attribute dictionary
- **Self-closing tags** - `<br />`, `<img />` detected and marked
- **Void elements** - HTML void elements (`img`, `br`, `input`, etc.) automatically recognized
- **Comments** - `<!-- comment -->` parsed as `CommentNode`
- **CDATA sections** - `<![CDATA[...]]>` supported
- **DOCTYPE declarations** - `<!DOCTYPE html>` parsed as `DocumentTypeNode`
- **SVG/MathML** - Delegates to XML parser for proper namespace handling
- **Script/style preservation** - Content inside `<script>` and `<style>` preserved exactly

**Node types**:

:::C:Codeblock {lang="csharp"}
// Core nodes from Femur.Markup.Abstractions
DocumentNode     // Root document
ElementNode      // HTML elements (<div>, <p>, etc.)
  ├─ TagName: string
  ├─ Attributes: Dictionary<string, string>
  ├─ IsSelfClosing: bool
  └─ IsVoidElement: bool

TextNode         // Text content
CommentNode      // <!-- comments -->
CDataNode        // <![CDATA[...]]>
DocumentTypeNode // <!DOCTYPE html>
XmlElementNode   // SVG/MathML elements
:::

**Usage example**: 
""";

    private string ParseAndRender(string markdown)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));
        var parser = new MarkdownParser(stream);
        var document = parser.Parse();
        return this._renderer.Render(document);
    }

    private static string Dump(Node node, int indent = 0)
    {
        var sb = new System.Text.StringBuilder();
        var pad = new string(' ', indent * 2);

        var desc = node switch
        {
            MarkdownDocumentNode   => "[Document]",
            HeadingNode h          => $"[Heading level={h.Level}]",
            ParagraphNode          => "[Paragraph]",
            ListNode l             => $"[List ordered={l.IsOrdered} loose={l.IsLoose}]",
            ListItemNode           => "[ListItem]",
            BlockQuoteNode         => "[BlockQuote]",
            CodeBlockNode c        => $"[CodeBlock fenced={c.IsFenced} info={c.Info ?? "null"} content={Escape(c.Content)}]",
            FencedDivNode f        => $"[FencedDiv tag={f.Tag ?? "null"} attrs={f.Attributes} rawContent={Escape(f.RawContent)}]",
            StrongEmphasisNode     => "[Strong]",
            EmphasisNode           => "[Emphasis]",
            CodeSpanNode cs        => $"[CodeSpan content={Escape(cs.Content)}]",
            LinkNode lk            => $"[Link url={lk.Url}]",
            ImageNode img          => $"[Image url={img.Url}]",
            HardLineBreakNode      => "[HardBreak]",
            SoftLineBreakNode      => "[SoftBreak]",
            MarkdownTextNode t     => $"[Text content={Escape(t.Content)}]",
            _                      => $"[Unknown {node.GetType().Name}]"
        };

        sb.AppendLine($"{pad}{desc}");

        if (node is ParentNode parent)
        {
            foreach (var child in parent.Children)
                sb.Append(Dump(child, indent + 1));
        }

        return sb.ToString();
    }

    private static string Escape(string? s) =>
        s == null ? "null" : "\"" + s.Replace("\n", "\\n").Replace("\r", "\\r") + "\"";

    [Fact]
    public void Diagnostic_DumpAstAndHtml()
    {
        var doc = MarkdownParser.Parse(Markdown);
        var html = ParseAndRender(Markdown);

        var astDump = Dump(doc);

        Console.WriteLine("========== AST ==========");
        Console.WriteLine(astDump);
        Console.WriteLine("========== HTML ==========");
        Console.WriteLine(html);
        Console.WriteLine("==========================");

        // Always passes — output is for inspection only
        Assert.NotNull(doc);
    }

    [Fact]
    public void Diagnostic_CheckUnicodeInRawContent()
    {
        var doc = MarkdownParser.Parse(Markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Print codepoints for first 20 chars of each line that should contain box chars
        foreach (var line in div.RawContent.Split('\n'))
        {
            if (line.TrimStart().StartsWith("├") || line.TrimStart().StartsWith("└"))
            {
                Console.WriteLine($"LINE (len={line.Length}): {line}");
                foreach (var ch in line.Take(6))
                    Console.WriteLine($"  U+{(int)ch:X4} '{ch}'");
                break;
            }
        }

        // Check whether box-drawing chars survived — U+251C ├, U+2514 └, U+2500 ─
        var hasBoxChars = div.RawContent.Contains('\u251c') || div.RawContent.Contains('\u2514');
        var hasMojibake = div.RawContent.Contains('\ufffd'); // replacement char

        Console.WriteLine($"HasBoxDrawingChars: {hasBoxChars}");
        Console.WriteLine($"HasReplacementChars (mojibake): {hasMojibake}");
        Console.WriteLine($"RawContent length: {div.RawContent.Length}");

        // Print the hex of the whole raw content for inspection
        var hexLines = div.RawContent
            .Select((c, i) => (i, c))
            .Where(x => x.c > 127)
            .Select(x => $"  [{x.i}] U+{(int)x.c:X4} '{x.c}'");
        Console.WriteLine("Non-ASCII chars in RawContent:");
        foreach (var h in hexLines) Console.WriteLine(h);

        Assert.NotNull(div);
    }
}
