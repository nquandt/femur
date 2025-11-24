using System.Text;

namespace StreamParserTests;

public class GetAbsolutePositionTests : IClassFixture<TestFixture>, IDisposable
{
    public GetAbsolutePositionTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region GetAbsolutePosition Method

    [Fact]
    public void GetAbsolutePosition_AtStart_ReturnsZero()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();

        // Act
        var position = parser.GetAbsolutePosition();

        // Assert
        Assert.Equal(0, position);
    }

    [Fact]
    public void GetAbsolutePosition_AfterConsumingBytes_ReturnsCorrectPosition()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();
        parser.Position = 2;

        // Act
        var position = parser.GetAbsolutePosition();

        // Assert
        Assert.Equal(2, position);
    }

    [Fact]
    public void GetAbsolutePosition_AcrossBufferBoundaries_ReturnsCorrectPosition()
    {
        // Arrange
        var content = new string('x', 5000);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream, bufferSize: 1024);
        _ = parser.ReadMore();

        // Consume first buffer
        parser.Position = parser.Length;
        var firstBufferSize = parser.Length;
        _ = parser.ReadMore(); // Read second buffer

        // Act
        var position = parser.GetAbsolutePosition();

        // Assert
        Assert.Equal(firstBufferSize, position); // Should be at start of second buffer
    }

    #endregion
}

