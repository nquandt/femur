using Femur.Markdown.Abstractions.Nodes;
using Femur.Markdown.Parser;
using Xunit;

namespace Femur.Markdown.Tests;

/// <summary>
/// Tests for fenced div parsing (Pandoc fenced_divs extension).
/// Verifies block container syntax, attribute parsing, and nested structures.
/// </summary>
public class FencedDivTests
{
    [Fact]
    public void Parse_SimpleFencedDiv_CreatesNode()
    {
        // Arrange
        var markdown = """
::: {.note}
This is a note.
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);

        // Assert
        Assert.True(doc.HasChildren);
        var div = doc.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(div);
        Assert.Single(div.ParsedAttributes.Classes);
        Assert.Contains("note", div.ParsedAttributes.Classes);
    }

    [Fact]
    public void Parse_FencedDivWithId_ParsesIdAttribute()
    {
        // Arrange
        var markdown = """
::: {#special .sidebar}
Content
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.Equal("special", div.ParsedAttributes.Id);
        Assert.Contains("sidebar", div.ParsedAttributes.Classes);
    }

    [Fact]
    public void Parse_FencedDivWithMultipleClasses_ParsesAllClasses()
    {
        // Arrange
        var markdown = """
::: {.class1 .class2 .class3}
Content
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.Equal(3, div.ParsedAttributes.Classes.Count);
        Assert.Contains("class1", div.ParsedAttributes.Classes);
        Assert.Contains("class2", div.ParsedAttributes.Classes);
        Assert.Contains("class3", div.ParsedAttributes.Classes);
    }

    [Fact]
    public void Parse_FencedDivWithKeyValueAttributes_ParsesAttributes()
    {
        // Arrange
        var markdown = """
::: {lang="csharp" version="1.0"}
Content
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.Equal("csharp", div.ParsedAttributes.KeyValueAttributes["lang"]);
        Assert.Equal("1.0", div.ParsedAttributes.KeyValueAttributes["version"]);
    }

    [Fact]
    public void Parse_FencedDivWithMixedAttributes_ParsesAll()
    {
        // Arrange
        var markdown = """
::: {#myid .class1 .class2 lang="python" data="test"}
Content
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.Equal("myid", div.ParsedAttributes.Id);
        Assert.Equal(2, div.ParsedAttributes.Classes.Count);
        Assert.Equal(2, div.ParsedAttributes.KeyValueAttributes.Count);
    }

    [Fact]
    public void Parse_FencedDivWithParagraphContent_ParsesContent()
    {
        // Arrange
        var markdown = """
::: {.container}
This is a paragraph inside the div.
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.True(div.HasChildren);
        var paragraph = div.Children.OfType<ParagraphNode>().FirstOrDefault();
        Assert.NotNull(paragraph);
    }

    [Fact]
    public void Parse_FencedDivWithHeading_ParsesHeadingContent()
    {
        // Arrange
        var markdown = """
::: {.section}
# Heading

Paragraph text
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        var heading = div.Children.OfType<HeadingNode>().FirstOrDefault();
        Assert.NotNull(heading);
        Assert.Equal(1, heading.Level);
    }

    [Fact]
    public void Parse_FencedDivWithCodeBlock_ParsesCodeBlock()
    {
        // Arrange
        var markdown = """
::: {.code}
```csharp
var x = 42;
```
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        var codeBlock = div.Children.OfType<CodeBlockNode>().FirstOrDefault();
        Assert.NotNull(codeBlock);
        Assert.Contains("x = 42", codeBlock.Content);
    }

    [Fact]
    public void Parse_FencedDivWithIndentedCodeBlock_ParsesIndentedCodeBlock()
    {
        // Arrange
        var markdown = """
::: {lang="csharp"}
    public static void main() {}
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.Equal("csharp", div.ParsedAttributes.KeyValueAttributes["lang"]);
        var codeBlock = div.Children.OfType<CodeBlockNode>().FirstOrDefault();
        Assert.NotNull(codeBlock);
        Assert.False(codeBlock.IsFenced); // Indented code blocks are not fenced
        Assert.Contains("public static void main() {}", codeBlock.Content);
    }

    [Fact]
    public void Parse_NamedFencedDivWithoutAttributes_ParsesName()
    {
        // Arrange
        var markdown = """
::: warning
I am warning text
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.Equal("warning", div.Tag);
        Assert.True(string.IsNullOrEmpty(div.Attributes));
        Assert.True(div.HasChildren);
        var paragraph = div.Children.OfType<ParagraphNode>().FirstOrDefault();
        Assert.NotNull(paragraph);
        var textNode = paragraph.Children.OfType<MarkdownTextNode>().FirstOrDefault();
        Assert.NotNull(textNode);
        Assert.Contains("I am warning text", textNode.Content);
    }

    [Fact]
    public void Parse_NamedFencedDivWithAttributes_ParsesNameAndAttributes()
    {
        // Arrange
        var markdown = """
:::C:Codeblock {lang="csharp"}
    public static void main() {}
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.Equal("C:Codeblock", div.Tag);
        Assert.Equal("{lang=\"csharp\"}", div.Attributes);
        Assert.Equal("csharp", div.ParsedAttributes.KeyValueAttributes["lang"]);
        var codeBlock = div.Children.OfType<CodeBlockNode>().FirstOrDefault();
        Assert.NotNull(codeBlock);
        Assert.Contains("public static void main() {}", codeBlock.Content);
    }

    [Fact]
    public void Parse_NamedFencedDivSimpleName_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
:::a
My Button
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.Equal("a", div.Tag);
        Assert.True(string.IsNullOrEmpty(div.Attributes));
        Assert.True(div.HasChildren);
        var paragraph = div.Children.OfType<ParagraphNode>().FirstOrDefault();
        Assert.NotNull(paragraph);
        var textNode = paragraph.Children.OfType<MarkdownTextNode>().FirstOrDefault();
        Assert.NotNull(textNode);
        Assert.Contains("My Button", textNode.Content);
    }

    [Fact]
    public void Parse_FencedDivWithList_ParsesList()
    {
        // Arrange
        var markdown = """
::: {.items}
- Item 1
- Item 2
- Item 3
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        var list = div.Children.OfType<ListNode>().FirstOrDefault();
        Assert.NotNull(list);
    }

    [Fact]
    public void Parse_NestedFencedDivs_ParsesNesting()
    {
        // Arrange
        var markdown = """
::::: {.outer}
Outer content
::: {.inner}
Inner content
:::
More outer content
:::::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var outerDiv = doc.Children.OfType<FencedDivNode>().FirstOrDefault();

        // Assert
        Assert.NotNull(outerDiv);
        Assert.Contains("outer", outerDiv.ParsedAttributes.Classes);
        
        var innerDiv = outerDiv.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(innerDiv);
        Assert.Contains("inner", innerDiv.ParsedAttributes.Classes);
    }

    [Fact]
    public void Parse_MultipleNestedDivs_ParsesAllLevels()
    {
        // Arrange
        var markdown = """
:::::: {.level1}
::: {.level2a}
Content A
:::
::: {.level2b}
Content B
:::
::::::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var level1 = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        var level2Divs = level1.Children.OfType<FencedDivNode>().ToList();
        Assert.Equal(2, level2Divs.Count);
        Assert.Contains("level2a", level2Divs[0].ParsedAttributes.Classes);
        Assert.Contains("level2b", level2Divs[1].ParsedAttributes.Classes);
    }

    [Fact]
    public void Parse_FencedDivNoAttributes_NotParsedAsDiv()
    {
        // Arrange
        var markdown = """
:::
This should not be a div.
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);

        // Assert
        var div = doc.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.Null(div);
    }

    [Fact]
    public void Parse_FencedDivWithoutClosing_NotParsed()
    {
        // Arrange
        var markdown = """
::: {.incomplete}
This div is never closed.
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);

        // Assert
        var div = doc.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.Null(div);
    }

    [Fact]
    public void Parse_FencedDivClosingFenceVariableLength_Accepted()
    {
        // Arrange
        var markdown = """
::::: {.outer}
Content
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);

        // Assert
        var div = doc.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(div);
        Assert.Equal(5, div.OpeningFenceLength);
    }

    [Fact]
    public void Parse_FencedDivEmptyContent_Parsed()
    {
        // Arrange
        var markdown = """
::: {.empty}
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);

        // Assert
        var div = doc.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(div);
        Assert.False(div.HasChildren);
    }

    [Fact]
    public void Parse_FencedDivWithBlockQuote_ParsesBlockQuote()
    {
        // Arrange
        var markdown = """
::: {.quote-container}
> This is a block quote
> inside a div
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        var blockQuote = div.Children.OfType<BlockQuoteNode>().FirstOrDefault();
        Assert.NotNull(blockQuote);
    }

    [Fact]
    public void Parse_FencedDivWithThematicBreak_ParsesThematicBreak()
    {
        // Arrange
        var markdown = """
::: {.section}
Content before

---

Content after
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        var thematicBreak = div.Children.OfType<ThematicBreakNode>().FirstOrDefault();
        Assert.NotNull(thematicBreak);
    }

    [Fact]
    public void Parse_FencedDivWithMultipleParagraphs_ParsesAll()
    {
        // Arrange
        var markdown = """
::: {.content}
First paragraph.

Second paragraph.

Third paragraph.
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        var paragraphs = div.Children.OfType<ParagraphNode>().ToList();
        Assert.Equal(3, paragraphs.Count);
    }

    [Fact]
    public void Parse_FencedDivAttributesPreserved_RawString()
    {
        // Arrange
        var markdown = """
::: {#test .class1 key="value"}
Content
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.Equal("{#test .class1 key=\"value\"}", div.Attributes);
    }

    [Fact]
    public void Parse_FencedDivComplexNesting_PreservesStructure()
    {
        // Arrange
        var markdown = """
::: {.article}
::: {.header}
# Title
:::
::: {.body}
Paragraph
::: {.aside}
Side note
:::
:::
::: {.footer}
End
:::
:::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var article = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        var directChildren = article.Children.OfType<FencedDivNode>().ToList();
        Assert.Equal(3, directChildren.Count); // header, body, footer
        
        var body = directChildren.FirstOrDefault(d => d.ParsedAttributes.Classes.Contains("body"));
        Assert.NotNull(body);
        var aside = body.Children.OfType<FencedDivNode>().FirstOrDefault();
        Assert.NotNull(aside);
        Assert.Contains("aside", aside.ParsedAttributes.Classes);
    }

    [Fact]
    public void Parse_FencedDivFollowedByParagraph_Separate()
    {
        // Arrange
        var markdown = """
::: {.div}
Inside div
:::

Outside paragraph
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);

        // Assert
        var div = doc.Children.OfType<FencedDivNode>().FirstOrDefault();
        var paragraph = doc.Children.OfType<ParagraphNode>().FirstOrDefault();
        Assert.NotNull(div);
        Assert.NotNull(paragraph);
    }

    [Fact]
    public void Parse_FencedDivWithLongFence_StoresLength()
    {
        // Arrange
        var markdown = """
:::::::::::::::::: {.test}
Content
::::::::::::::::::
""";

        // Act
        var doc = MarkdownParser.Parse(markdown);
        var div = doc.Children.OfType<FencedDivNode>().First();

        // Assert
        Assert.Equal(18, div.OpeningFenceLength);
    }
}
