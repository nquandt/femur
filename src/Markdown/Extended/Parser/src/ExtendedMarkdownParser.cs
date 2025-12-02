using System.Text;
using Femur.Markdown.Abstractions.Nodes;
using Femur.Markdown.Extended.Abstractions.Nodes;
using Femur.Markdown.Parser;
using YamlDotNet.RepresentationModel;

namespace Femur.Markdown.Extended.Parser;

/// <summary>
/// Extended Markdown parser that adds YAML frontmatter support to the CommonMark specification.
/// Extends MarkdownParser to handle frontmatter as a preprocessing block before markdown content.
/// 
/// PARSING STRATEGY:
/// - InitializeParsing: Checks if stream starts with "---" and reads frontmatter block if present
/// - Frontmatter is treated like any other block structure (line-by-line reading)
/// - Remaining content flows through standard markdown parsing via ProcessCharacter
/// - Both frontmatter and markdown content share the same stream buffer
/// 
/// This approach maintains true streaming semantics and allows composition:
/// - Frontmatter extraction is just preprocessing, not a separate parsing phase
/// - Markdown parsing continues naturally after frontmatter
/// - Can be extended further with additional preprocessing filters
/// </summary>
public class ExtendedMarkdownParser : MarkdownParser
{
    private ExtendedMarkdownDocumentNode? _extendedDocument;

    /// <summary>
    /// Initializes a new instance of the ExtendedMarkdownParser.
    /// </summary>
    /// <param name="stream">The stream to parse</param>
    /// <param name="bufferSize">Size of the buffer for reading chunks (default 4096)</param>
    public ExtendedMarkdownParser(Stream stream, int bufferSize = 4096)
        : base(stream, bufferSize)
    {
    }

    /// <summary>
    /// Parses the stream and returns an ExtendedMarkdownDocumentNode with frontmatter support.
    /// </summary>
    /// <returns>An ExtendedMarkdownDocumentNode containing parsed markdown and frontmatter.</returns>
    public new ExtendedMarkdownDocumentNode Parse()
    {
        var document = base.Parse();
        return (ExtendedMarkdownDocumentNode)document;
    }

    /// <summary>
    /// Parses a stream and returns an ExtendedMarkdownDocumentNode with frontmatter support.
    /// </summary>
    /// <param name="stream">The stream to parse</param>
    /// <returns>An ExtendedMarkdownDocumentNode containing parsed markdown and frontmatter.</returns>
    public static new ExtendedMarkdownDocumentNode Parse(Stream stream)
    {
        var parser = new ExtendedMarkdownParser(stream);
        return parser.Parse();
    }

    /// <summary>
    /// Parses byte array and returns an ExtendedMarkdownDocumentNode with frontmatter support.
    /// </summary>
    /// <param name="bytes">The bytes to parse</param>
    /// <returns>An ExtendedMarkdownDocumentNode containing parsed markdown and frontmatter.</returns>
    public static new ExtendedMarkdownDocumentNode Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return Parse(stream);
    }

    /// <summary>
    /// Parses a string and returns an ExtendedMarkdownDocumentNode with frontmatter support.
    /// </summary>
    /// <param name="markdown">The markdown string to parse</param>
    /// <returns>An ExtendedMarkdownDocumentNode containing parsed markdown and frontmatter.</returns>
    public static new ExtendedMarkdownDocumentNode Parse(string markdown)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(markdown);
        return Parse(bytes);
    }

    /// <summary>
    /// Creates an ExtendedMarkdownDocumentNode instead of base MarkdownDocumentNode.
    /// </summary>
    protected override MarkdownDocumentNode CreateDocument()
    {
        this._extendedDocument = new ExtendedMarkdownDocumentNode();
        return this._extendedDocument;
    }

    /// <summary>
    /// Initializes parsing and handles frontmatter extraction before markdown parsing begins.
    /// </summary>
    protected override void InitializeParsing(MarkdownDocumentNode document)
    {
        if (this._extendedDocument == null)
        {
            return;
        }

        // Check if we start with frontmatter delimiter
        var firstLineBuilder = new StringBuilder();
        while (this.Position < this.Length && this.Buffer[this.Position] != '\n')
        {
            if (this.Buffer[this.Position] != '\r')
            {
                firstLineBuilder.Append(this.Buffer[this.Position]);
            }

            this.Position++;
        }

        // Skip the newline
        if (this.Position < this.Length && this.Buffer[this.Position] == '\n')
        {
            this.Position++;
        }

        var firstLine = firstLineBuilder.ToString();

        // If first line is EXACTLY "---" (no leading whitespace), try to read frontmatter
        if (firstLine == "---")
        {
            if (this.TryExtractFrontmatter(this._extendedDocument))
            {
                // Frontmatter was successfully extracted, position is now after closing "---"
                // Continue with normal markdown parsing
            }
            else
            {
                // No valid frontmatter found, reset position and treat first line as markdown
                this.Position = 0;
            }
        }
        else
        {
            // No frontmatter, reset to beginning for markdown parsing
            this.Position = 0;
        }

        // Now call base initialization which will process markdown blocks normally
        base.InitializeParsing(document);
    }

    /// <summary>
    /// Attempts to extract frontmatter from the stream starting at current position.
    /// Reads lines until closing "---" delimiter is found.
    /// Creates and adds a FrontMatterBlockNode to the document.
    /// </summary>
    /// <returns>True if valid frontmatter was extracted, false otherwise</returns>
    private bool TryExtractFrontmatter(ExtendedMarkdownDocumentNode document)
    {
        var frontMatterBuilder = new StringBuilder();
        string? line;
        var foundClosing = false;

        while ((line = this.ReadLine()) != null)
        {
            if (line.Trim() == "---")
            {
                foundClosing = true;
                break;
            }

            frontMatterBuilder.AppendLine(line);
        }

        if (!foundClosing)
        {
            return false;
        }

        var frontMatterText = frontMatterBuilder.ToString().TrimEnd();
        var parsedData = ParseYamlFrontMatter(frontMatterText);

        // Create the FrontMatterBlockNode
        var frontMatterNode = new FrontMatterBlockNode
        {
            RawContent = frontMatterText,
            ParsedData = parsedData
        };

        // Add to document
        document.FrontMatterBlock = frontMatterNode;
        document.Children.Insert(0, frontMatterNode);

        return true;
    }

    /// <summary>
    /// Reads a line from the current buffer position, handling different line endings.
    /// Returns null when stream is exhausted.
    /// </summary>
    private string? ReadLine()
    {
        var lineBuilder = new StringBuilder();

        while (this.ReadMore())
        {
            if (this.Position >= this.Length)
            {
                continue;
            }

            var ch = this.Buffer[this.Position];
            this.Position++;

            if (ch == '\n')
            {
                // End of line
                if (lineBuilder.Length > 0 && lineBuilder[^1] == '\r')
                {
                    lineBuilder.Length--; // Remove trailing \r
                }

                return lineBuilder.ToString();
            }

            lineBuilder.Append(ch);
        }

        // End of stream
        return lineBuilder.Length > 0 ? lineBuilder.ToString() : null;
    }

    /// <summary>
    /// Parses YAML frontmatter into a dictionary.
    /// </summary>
    private static Dictionary<string, object>? ParseYamlFrontMatter(string? yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText))
        {
            // Empty frontmatter returns an empty dictionary (not null)
            return new Dictionary<string, object>();
        }

        try
        {
            var dict = new Dictionary<string, object>();
            var yaml = new YamlStream();
            yaml.Load(new StringReader(yamlText));

            if (yaml.Documents.Count == 0)
            {
                return dict;
            }

            var root = yaml.Documents[0].RootNode as YamlMappingNode;
            if (root == null)
            {
                return dict;
            }

            foreach (var entry in root.Children)
            {
                var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
                var value = ParseYamlValue(entry.Value);
                dict[key] = value;
            }

            return dict;
        }
        catch
        {
            // If YAML parsing fails, return null
            // The raw text is still available in FrontMatterRaw for debugging
            return null;
        }
    }

    /// <summary>
    /// Recursively parses YAML values into appropriate .NET types.
    /// </summary>
    private static object ParseYamlValue(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => scalar.Value ?? string.Empty,
            YamlSequenceNode sequence => ParseYamlSequence(sequence),
            YamlMappingNode mapping => ParseYamlMapping(mapping),
            _ => node.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Parses a YAML sequence (list) into a List&lt;object&gt;.
    /// </summary>

    private static List<object> ParseYamlSequence(YamlSequenceNode sequence)
    {
        var list = new List<object>();
        foreach (var node in sequence.Children)
        {
            list.Add(ParseYamlValue(node));
        }

        return list;
    }

    /// <summary>
    /// Parses a YAML mapping (dictionary) into a Dictionary&lt;string, object&gt;.
    /// </summary>

    private static Dictionary<string, object> ParseYamlMapping(YamlMappingNode mapping)
    {
        var dict = new Dictionary<string, object>();
        foreach (var entry in mapping.Children)
        {
            var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
            var value = ParseYamlValue(entry.Value);
            dict[key] = value;
        }

        return dict;
    }
}
