using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Femur.Markdown.Parser;
using Femur.Markdown.Renderer;
using Femur.Markdown.Abstractions.Nodes;
using Markdig;
using Markdig.Renderers;

namespace MarkdownBenchmarks;

/// <summary>
/// Compares Femur markdown parsing and rendering against Markdig for features
/// that Femur supports: paragraphs, headings (ATX + Setext), emphasis, strong,
/// code spans, fenced code blocks, indented code blocks, links, images,
/// block quotes, ordered/unordered lists, thematic breaks, and hard/soft line breaks.
///
/// Markdig is configured with a plain CommonMark-equivalent pipeline (no extensions)
/// to keep the comparison fair against Femur's core parser.
///
/// Input files are embedded resources loaded to <c>byte[]</c> in GlobalSetup
/// so that IO is entirely outside the measured hot path.
///
/// Three input sizes:
///   small  — ~200 bytes  (a single paragraph with common inline syntax)
///   medium — ~2.5 KB     (a realistic blog-post excerpt with all major block types)
///   large  — ~20 KB      (the medium document repeated across multiple sections)
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class MarkdownBenchmarks
{
    // -------------------------------------------------------------------------
    // Markdig pipeline — CommonMark only, no extensions
    // -------------------------------------------------------------------------

    private static readonly MarkdownPipeline MarkdigPipeline =
        new MarkdownPipelineBuilder().Build();

    // -------------------------------------------------------------------------
    // In-memory input bytes (populated in GlobalSetup — no IO in hot path)
    // -------------------------------------------------------------------------

    private byte[] _smallBytes = [];
    private byte[] _mediumBytes = [];
    private byte[] _largeBytes = [];

    // -------------------------------------------------------------------------
    // Pre-parsed ASTs (used for render-only benchmarks)
    // -------------------------------------------------------------------------

    private MarkdownDocumentNode _femurSmallAst = null!;
    private MarkdownDocumentNode _femurMediumAst = null!;
    private MarkdownDocumentNode _femurLargeAst = null!;

    private global::Markdig.Syntax.MarkdownDocument _markdigSmallAst = null!;
    private global::Markdig.Syntax.MarkdownDocument _markdigMediumAst = null!;
    private global::Markdig.Syntax.MarkdownDocument _markdigLargeAst = null!;

    // Reusable Femur renderer (stateless between Render calls)
    private MarkdownHtmlRenderer _femurRenderer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallBytes = LoadEmbeddedResource("inputs.small.md");
        _mediumBytes = LoadEmbeddedResource("inputs.medium.md");
        _largeBytes = LoadEmbeddedResource("inputs.large.md");

        _femurRenderer = new MarkdownHtmlRenderer();

        _femurSmallAst = FemurParse(_smallBytes);
        _femurMediumAst = FemurParse(_mediumBytes);
        _femurLargeAst = FemurParse(_largeBytes);

        _markdigSmallAst = MarkdigParse(_smallBytes);
        _markdigMediumAst = MarkdigParse(_mediumBytes);
        _markdigLargeAst = MarkdigParse(_largeBytes);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Loads an embedded resource by its dot-separated name suffix.</summary>
    private static byte[] LoadEmbeddedResource(string nameSuffix)
    {
        var assembly = typeof(MarkdownBenchmarks).Assembly;
        var fullName = Array.Find(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith(nameSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded resource ending in '{nameSuffix}' not found.");

        using var stream = assembly.GetManifestResourceStream(fullName)!;
        var bytes = new byte[stream.Length];
        _ = stream.Read(bytes, 0, bytes.Length);
        return bytes;
    }

    private static MarkdownDocumentNode FemurParse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return new MarkdownParser(stream).Parse();
    }

    private static global::Markdig.Syntax.MarkdownDocument MarkdigParse(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        return global::Markdig.Markdown.Parse(text, MarkdigPipeline);
    }

    private static string MarkdigRender(global::Markdig.Syntax.MarkdownDocument ast)
    {
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        var renderer = new HtmlRenderer(writer);
        MarkdigPipeline.Setup(renderer);
        renderer.Render(ast);
        writer.Flush();
        return sb.ToString();
    }

    // =========================================================================
    // Parse-only
    // =========================================================================

    [Benchmark(Description = "Femur  parse small")]
    [BenchmarkCategory("Parse", "Small")]
    public MarkdownDocumentNode Femur_Parse_Small() => FemurParse(_smallBytes);

    [Benchmark(Description = "Markdig parse small")]
    [BenchmarkCategory("Parse", "Small")]
    public global::Markdig.Syntax.MarkdownDocument Markdig_Parse_Small() => MarkdigParse(_smallBytes);

    [Benchmark(Description = "Femur  parse medium")]
    [BenchmarkCategory("Parse", "Medium")]
    public MarkdownDocumentNode Femur_Parse_Medium() => FemurParse(_mediumBytes);

    [Benchmark(Description = "Markdig parse medium")]
    [BenchmarkCategory("Parse", "Medium")]
    public global::Markdig.Syntax.MarkdownDocument Markdig_Parse_Medium() => MarkdigParse(_mediumBytes);

    [Benchmark(Description = "Femur  parse large")]
    [BenchmarkCategory("Parse", "Large")]
    public MarkdownDocumentNode Femur_Parse_Large() => FemurParse(_largeBytes);

    [Benchmark(Description = "Markdig parse large")]
    [BenchmarkCategory("Parse", "Large")]
    public global::Markdig.Syntax.MarkdownDocument Markdig_Parse_Large() => MarkdigParse(_largeBytes);

    // =========================================================================
    // Render-only (pre-parsed AST → HTML, no parsing in hot path)
    // =========================================================================

    [Benchmark(Description = "Femur  render small")]
    [BenchmarkCategory("Render", "Small")]
    public string Femur_Render_Small() => _femurRenderer.Render(_femurSmallAst);

    [Benchmark(Description = "Markdig render small")]
    [BenchmarkCategory("Render", "Small")]
    public string Markdig_Render_Small() => MarkdigRender(_markdigSmallAst);

    [Benchmark(Description = "Femur  render medium")]
    [BenchmarkCategory("Render", "Medium")]
    public string Femur_Render_Medium() => _femurRenderer.Render(_femurMediumAst);

    [Benchmark(Description = "Markdig render medium")]
    [BenchmarkCategory("Render", "Medium")]
    public string Markdig_Render_Medium() => MarkdigRender(_markdigMediumAst);

    [Benchmark(Description = "Femur  render large")]
    [BenchmarkCategory("Render", "Large")]
    public string Femur_Render_Large() => _femurRenderer.Render(_femurLargeAst);

    [Benchmark(Description = "Markdig render large")]
    [BenchmarkCategory("Render", "Large")]
    public string Markdig_Render_Large() => MarkdigRender(_markdigLargeAst);

    // =========================================================================
    // Parse + Render (end-to-end)
    // =========================================================================

    [Benchmark(Description = "Femur  parse+render small")]
    [BenchmarkCategory("ParseRender", "Small")]
    public string Femur_ParseRender_Small()
    {
        var ast = FemurParse(_smallBytes);
        return _femurRenderer.Render(ast);
    }

    [Benchmark(Description = "Markdig parse+render small")]
    [BenchmarkCategory("ParseRender", "Small")]
    public string Markdig_ParseRender_Small()
    {
        var text = Encoding.UTF8.GetString(_smallBytes);
        return global::Markdig.Markdown.ToHtml(text, MarkdigPipeline);
    }

    [Benchmark(Description = "Femur  parse+render medium")]
    [BenchmarkCategory("ParseRender", "Medium")]
    public string Femur_ParseRender_Medium()
    {
        var ast = FemurParse(_mediumBytes);
        return _femurRenderer.Render(ast);
    }

    [Benchmark(Description = "Markdig parse+render medium")]
    [BenchmarkCategory("ParseRender", "Medium")]
    public string Markdig_ParseRender_Medium()
    {
        var text = Encoding.UTF8.GetString(_mediumBytes);
        return global::Markdig.Markdown.ToHtml(text, MarkdigPipeline);
    }

    [Benchmark(Description = "Femur  parse+render large")]
    [BenchmarkCategory("ParseRender", "Large")]
    public string Femur_ParseRender_Large()
    {
        var ast = FemurParse(_largeBytes);
        return _femurRenderer.Render(ast);
    }

    [Benchmark(Description = "Markdig parse+render large")]
    [BenchmarkCategory("ParseRender", "Large")]
    public string Markdig_ParseRender_Large()
    {
        var text = Encoding.UTF8.GetString(_largeBytes);
        return global::Markdig.Markdown.ToHtml(text, MarkdigPipeline);
    }
}
