using System.Text;

namespace StreamParserTests;

public class TemplateMethodTests : IClassFixture<TestFixture>, IDisposable
{
    public TemplateMethodTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Template Method Pattern

    [Fact]
    public void Parse_WithEmptyStream_ReturnsEmptyDocument()
    {
        // Arrange
        var stream = new MemoryStream();
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        var document = parser.Parse();

        // Assert
        Assert.NotNull(document);
        Assert.Equal(0, document.Content.Length);
    }

    [Fact]
    public void Parse_CallsCreateDocument()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        var document = parser.Parse();

        // Assert
        Assert.True(parser.CreateDocumentCalled);
        Assert.NotNull(document);
    }

    [Fact]
    public void Parse_CallsInitializeParsing()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        _ = parser.Parse();

        // Assert
        Assert.True(parser.InitializeParsingCalled);
    }

    [Fact]
    public void Parse_CallsProcessCharacterForEachChar()
    {
        // Arrange
        var content = "abc";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        _ = parser.Parse();

        // Assert
        Assert.Equal(content.Length, parser.ProcessCharacterCallCount);
    }

    [Fact]
    public void Parse_CallsCleanup()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        _ = parser.Parse();

        // Assert
        Assert.True(parser.CleanupCalled);
    }

    [Fact]
    public void Parse_WithLargeContent_ProcessesAllCharacters()
    {
        // Arrange
        var content = new string('x', 10000);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        _ = parser.Parse();

        // Assert
        Assert.Equal(content.Length, parser.ProcessCharacterCallCount);
    }

    #endregion
}

