using System.Text;

namespace StreamParserTests;

public class SkipWhitespaceTests : IClassFixture<TestFixture>, IDisposable
{
    public SkipWhitespaceTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region SkipWhitespace Method

    [Fact]
    public void SkipWhitespace_SkipsSpaces()
    {
        // Arrange
        var content = "   content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();

        // Act
        parser.SkipWhitespace();

        // Assert
        Assert.Equal(3, parser.Position);
        var ch = parser.Buffer[parser.Position];
        Assert.Equal('c', ch);
    }

    [Fact]
    public void SkipWhitespace_SkipsTabsAndNewlines()
    {
        // Arrange
        var content = "\t\n\r content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();

        // Act
        parser.SkipWhitespace();

        // Assert
        Assert.True(parser.Position > 0);
        var ch = parser.Buffer[parser.Position];
        Assert.Equal('c', ch);
    }

    [Fact]
    public void SkipWhitespace_WithNoWhitespace_DoesNotAdvance()
    {
        // Arrange
        var content = "content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();

        // Act
        parser.SkipWhitespace();

        // Assert
        Assert.Equal(0, parser.Position);
    }

    [Fact]
    public void SkipWhitespace_AtEndOfBuffer_ReadsMore()
    {
        // Arrange
        var content = new string(' ', 2000) + "content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream, bufferSize: 1024);
        _ = parser.ReadMore();

        // Act
        parser.SkipWhitespace();

        // Assert - Should have read more data and skipped whitespace
        var ch = parser.Buffer[parser.Position];
        Assert.Equal('c', ch);
    }

    #endregion
}

