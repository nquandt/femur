using Femur.Chtml.Parser;
using Femur.Markup.Abstractions.Nodes;
using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class CodeBlocksTests : IClassFixture<TestFixture>, IDisposable
{
    public CodeBlocksTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Code Blocks

    [Fact]
    public void Parse_CodeBlock_CreatesCodeNode()
    {
        // Arrange
        var html = "<p>Hello {user.Name}</p>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var p = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal(2, p.Children.Count);
        var text = Assert.IsType<TextNode>(p.Children[0]);
        Assert.Equal("Hello ", text.Content);
        var code = Assert.IsType<CodeNode>(p.Children[1]);
        Assert.Equal("user.Name", code.Content);
    }

    [Fact]
    public void Parse_CodeBlockWithNestedBraces_FirstBraceCloses()
    {
        // Arrange
        var html = "<p>{outer {inner value}}</p>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var p = Assert.IsType<ElementNode>(document.Children[0]);
        // Nested braces are NOT allowed - first '}' closes the block
        // So we get: code block with content "outer {inner value" and leftover "}"
        var codeNodes = p.Children.OfType<CodeNode>().ToList();
        Assert.Single(codeNodes);
        Assert.Equal("outer {inner value", codeNodes[0].Content);

        // The leftover '}' should be treated as text or cause parsing issues
        // Since we have "}" after the code block, it might be parsed as text
        var textNodes = p.Children.OfType<TextNode>().ToList();
        // Either the '}' is parsed as text, or it's ignored
        // The important thing is that nested braces don't work
    }

    [Fact]
    public void Parse_CodeBlockAtStart_ParsesCorrectly()
    {
        // Arrange
        var html = "{RenderChildren()}";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.Single(document.Children);
        var code = Assert.IsType<CodeNode>(document.Children[0]);
        Assert.Equal("RenderChildren()", code.Content);
    }

    [Fact]
    public void Parse_CodeBlockWithMultipleBraces_HandlesCorrectly()
    {
        // Arrange
        var html = "<p>{one} and {two}</p>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var p = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Equal(3, p.Children.Count);
        var text = Assert.IsType<TextNode>(p.Children[1]);
        Assert.Equal(" and ", text.Content);
        var code1 = Assert.IsType<CodeNode>(p.Children[0]);
        Assert.Equal("one", code1.Content);
        var code2 = Assert.IsType<CodeNode>(p.Children[2]);
        Assert.Equal("two", code2.Content);
    }

    [Fact]
    public void Parse_UnclosedCodeBlock_StillCreatesNode()
    {
        // Arrange
        var html = "<p>{unclosed code";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        var p = Assert.IsType<ElementNode>(document.Children[0]);
        Assert.Single(p.Children);
        var code = Assert.IsType<CodeNode>(p.Children[0]);
        Assert.Equal("unclosed code", code.Content);
    }

    #endregion
}

