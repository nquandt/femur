using System.Text;

namespace StreamParserTests;

public class BufferManagementTests : IClassFixture<TestFixture>, IDisposable
{
    public BufferManagementTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Buffer Management

    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new TestHelpers.TestStreamParser(null!));
        Assert.Equal("stream", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithStream_InitializesBuffer()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        // Act
        var parser = new TestHelpers.TestStreamParser(stream);

        // Assert
        Assert.NotNull(parser.Buffer);
        Assert.True(parser.Buffer.Length > 0);
    }

    [Fact]
    public void Constructor_WithCustomBufferSize_UsesCustomSize()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var customBufferSize = 8192;

        // Act
        var parser = new TestHelpers.TestStreamParser(stream, customBufferSize);

        // Assert
        Assert.True(parser.Buffer.Length >= customBufferSize);
    }

    [Fact]
    public void Constructor_InitializesStringBuilder()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        // Act
        var parser = new TestHelpers.TestStreamParser(stream);

        // Assert
        Assert.NotNull(parser.StringBuilder);
    }

    #endregion
}

