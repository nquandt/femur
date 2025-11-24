using System.Text;

namespace StreamParserTests;

public class ReadMoreTests : IClassFixture<TestFixture>, IDisposable
{
    public ReadMoreTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region ReadMore Method

    [Fact]
    public void ReadMore_WithDataAvailable_ReturnsTrue()
    {
        // Arrange
        var content = "test content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        var result = parser.ReadMore();

        // Assert
        Assert.True(result);
        Assert.Equal(content.Length, parser.Length);
        Assert.Equal(0, parser.Position);
    }

    [Fact]
    public void ReadMore_WithEmptyStream_ReturnsFalse()
    {
        // Arrange
        var stream = new MemoryStream();
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        var result = parser.ReadMore();

        // Assert
        Assert.False(result);
        Assert.Equal(0, parser.Length);
    }

    [Fact]
    public void ReadMore_WhenBufferExhausted_ReadsNextChunk()
    {
        // Arrange
        var content = new string('x', 5000); // Larger than default buffer
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream, bufferSize: 1024);

        // Act - Read first chunk
        _ = parser.ReadMore();
        var firstLength = parser.Length;
        var firstTotalChars = parser.TotalCharsRead;

        // Consume entire buffer
        parser.Position = parser.Length;

        // Read next chunk
        var result = parser.ReadMore();

        // Assert
        Assert.True(result);
        Assert.True(parser.TotalCharsRead > firstTotalChars);
        Assert.Equal(0, parser.Position);
    }

    [Fact]
    public void ReadMore_WhenPositionLessThanLength_ReturnsTrueWithoutReading()
    {
        // Arrange
        var content = "test";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();
        parser.Position = 2; // Still have data in buffer

        // Act
        var result = parser.ReadMore();

        // Assert
        Assert.True(result);
        Assert.Equal(2, parser.Position); // Position unchanged
    }

    #endregion
}

