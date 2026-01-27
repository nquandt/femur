using ChtmlParserInstance = Femur.Chtml.Parser.ChtmlParser;

namespace ChtmlParserTests;

public class YamlFrontMatterTests : IClassFixture<TestFixture>, IDisposable
{
    public YamlFrontMatterTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region YAML Front Matter

    [Fact]
    public void Parse_WithFrontMatter_ParsesFrontMatter()
    {
        // Arrange
        var html = @"---
Title: Test Page
Route: /test
---
<html><body>Content</body></html>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.NotNull(document.FrontMatter);
        Assert.Equal(2, document.FrontMatter!.Count);
        Assert.Equal("Test Page", document.FrontMatter["Title"]);
        Assert.Equal("/test", document.FrontMatter["Route"]);
        Assert.NotNull(document.FrontMatterRaw);
        Assert.NotEmpty(document.FrontMatterRaw);
    }

    [Fact]
    public void Parse_WithNestedFrontMatter_ParsesNestedStructure()
    {
        // Arrange
        var html = @"---
Components:
  Layout: components/layout
  Header: components/header
Props:
  Title: System.String
---
<html><body>Content</body></html>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.NotNull(document.FrontMatter);
        Assert.Equal(2, document.FrontMatter!.Count);
        var components = Assert.IsType<Dictionary<string, object>>(document.FrontMatter["Components"]);
        Assert.NotNull(components);
        Assert.Equal(2, components.Count);
        Assert.Equal("components/layout", components["Layout"]);
        Assert.Equal("components/header", components["Header"]);
    }

    [Fact]
    public void Parse_WithoutFrontMatter_DoesNotSetFrontMatter()
    {
        // Arrange
        var html = "<html><body>Content</body></html>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.Null(document.FrontMatter);
        Assert.Null(document.FrontMatterRaw);
    }

    [Fact]
    public void Parse_FrontMatterWithList_ParsesList()
    {
        // Arrange
        var html = @"---
Tags:
  - tag1
  - tag2
  - tag3
---
<html><body>Content</body></html>";

        // Act
        var document = ChtmlParserInstance.Parse(html);

        // Assert
        Assert.NotNull(document.FrontMatter);
        Assert.Single(document.FrontMatter!);
        var tags = Assert.IsType<List<object>>(document.FrontMatter["Tags"]);
        Assert.NotNull(tags);
        Assert.Equal(3, tags.Count);
        Assert.Equal("tag1", tags[0]);
    }

    #endregion
}

