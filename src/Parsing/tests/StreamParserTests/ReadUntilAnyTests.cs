using System.Text;

namespace StreamParserTests;

public class ReadUntilAnyTests : IClassFixture<TestFixture>, IDisposable
{
    public ReadUntilAnyTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region ReadUntilAny Method

    [Fact]
    public void ReadUntilAny_ReadsUntilAnyStopChar()
    {
        // Arrange
        var content = "hello,world";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();

        // Act
        var result = parser.ReadUntilAny([',', ';'], out var matchedChar);

        // Assert
        Assert.Equal("hello", result);
        Assert.Equal(',', matchedChar);
        Assert.Equal(6, parser.Position);
    }

    [Fact]
    public void ReadUntilAny_WithMultipleStopChars_MatchesFirst()
    {
        // Arrange
        var content = "hello;world";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();

        // Act
        var result = parser.ReadUntilAny([',', ';', ':'], out var matchedChar);

        // Assert
        Assert.Equal("hello", result);
        Assert.Equal(';', matchedChar);
    }

    [Fact]
    public void ReadUntilAny_WhenNoStopCharFound_SetsMatchedCharToNull()
    {
        // Arrange
        var content = "hello world";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream);
        _ = parser.ReadMore();

        // Act
        var result = parser.ReadUntilAny(['x', 'y', 'z'], out var matchedChar);

        // Assert
        Assert.Equal("hello world", result);
        Assert.Equal('\0', matchedChar);
    }

    [Fact]
    public void ReadUntilAny_AcrossBufferBoundaries_ReadsCorrectly()
    {
        // Arrange
        var part1 = new string('x', 1000);
        var part2 = "stop";
        var content = part1 + part2;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var parser = new TestHelpers.TestStreamParser(stream, bufferSize: 1024);
        _ = parser.ReadMore();

        // Act
        var result = parser.ReadUntilAny(['s', 't'], out var matchedChar);

        // Assert
        Assert.Equal(part1, result);
        Assert.Equal('s', matchedChar);
    }

    #endregion
}

