using System.Text;

namespace StreamParserTests;

public class ReadUntilTests : IClassFixture<TestFixture>, IDisposable
{
    public ReadUntilTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region ReadUntil Method

    [Fact]
    public void ReadUntil_ReadsUntilStopChar()
    {
        // Arrange
        var content = "hello world";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();

        // Act
        var result = parser.ReadUntil(' ');

        // Assert
        Assert.Equal("hello", result);
        Assert.Equal(6, parser.Position); // Position after space
    }

    [Fact]
    public void ReadUntil_WithIncludeStopChar_IncludesStopChar()
    {
        // Arrange
        var content = "hello world";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();

        // Act
        var result = parser.ReadUntil(' ', includeStopChar: true);

        // Assert
        Assert.Equal("hello ", result);
        // Position should be after the space character
        Assert.True(parser.Position >= 6);
    }

    [Fact]
    public void ReadUntil_AcrossBufferBoundaries_ReadsCorrectly()
    {
        // Arrange
        var part1 = new string('x', 1000);
        var part2 = "stop";
        var content = part1 + part2;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream, bufferSize: 1024);
        _ = parser.ReadMore();

        // Act
        var result = parser.ReadUntil('s');

        // Assert
        Assert.Equal(part1, result);
        Assert.True(parser.Position > 0);
    }

    [Fact]
    public void ReadUntil_WhenStopCharNotFound_ReadsToEnd()
    {
        // Arrange
        var content = "hello world";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();

        // Act
        var result = parser.ReadUntil('z');

        // Assert
        Assert.Equal("hello world", result);
    }

    #endregion
}

