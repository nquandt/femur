using System.Text;

namespace StreamParserTests;

public class CleanupTests : IClassFixture<TestFixture>, IDisposable
{
    public CleanupTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Cleanup

    [Fact]
    public void Cleanup_ReturnsBufferToPool()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var parser = new TestHelpers.TestStreamParser(stream);
        var buffer = parser.Buffer;

        // Act
        _ = parser.Parse(); // Calls Cleanup

        // Assert - Buffer should be returned (we can't directly verify this, but we can verify cleanup was called)
        Assert.True(parser.CleanupCalled);
    }

    [Fact]
    public void Dispose_CanBeCalledExplicitly()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        parser.Dispose();

        // Assert - CleanupCalled should be true since Dispose calls Cleanup
        Assert.True(parser.CleanupCalled);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act - Call dispose multiple times
        parser.Dispose();
        parser.Dispose();
        parser.Dispose();

        // Assert - Should not throw
        Assert.True(parser.CleanupCalled);
    }

    [Fact]
    public void UsingStatement_DisposesParserAutomatically()
    {
        // Arrange
        TestHelpers.TestStreamParser? parser = null;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        // Act
        using (parser = new TestHelpers.TestStreamParser(stream))
        {
            var doc = parser.Parse();
            Assert.NotNull(doc);
        }

        // Assert - Cleanup should have been called when exiting using block
        Assert.True(parser.CleanupCalled);
    }

    [Fact]
    public void UsingDeclaration_DisposesParserAutomatically()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
        TestHelpers.TestStreamParser? parser = null;

        // Act
        {
            using var localParser = new TestHelpers.TestStreamParser(stream);
            parser = localParser;
            var doc = parser.Parse();
            Assert.NotNull(doc);
        } // Dispose called here

        // Assert - Cleanup should have been called when exiting scope
        Assert.True(parser!.CleanupCalled);
    }

    [Fact]
    public void Parse_AutomaticallyDisposesResources()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        var document = parser.Parse();

        // Assert - Parse should call Cleanup automatically
        Assert.True(parser.CleanupCalled);
        Assert.NotNull(document);
    }

    [Fact]
    public void Dispose_BeforeParse_WorksCorrectly()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act - Dispose before calling Parse
        parser.Dispose();

        // Assert
        Assert.True(parser.CleanupCalled);
        // Note: Calling Parse after Dispose would fail, but that's expected behavior
    }

    [Fact]
    public void StreamReader_IsDisposedAfterParse()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        _ = parser.Parse();

        // Assert - The underlying stream should be disposed by the StreamReader
        // We can't directly test if Reader is disposed, but we can verify the stream is disposed
        _ = Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
    }

    [Fact]
    public void StreamReader_IsDisposedAfterExplicitDispose()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
        var parser = new TestHelpers.TestStreamParser(stream);

        // Act
        parser.Dispose();

        // Assert - The underlying stream should be disposed
        _ = Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
    }

    #endregion
}

