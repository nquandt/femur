
// ```test/Serialization.Tests/DefaultJsonSerializerTests.cs
using System.Text;

namespace Femur.Serialization.Tests;

public class DefaultJsonSerializerTests
{
    [Fact]
    public async Task SerializeAsync_ValidObject_ShouldSerializeToJson()
    {
        // Arrange
        var serializer = new DefaultJsonSerializer(null);
        var obj = new TestClass { Name = "Test" };
        using var stream = new MemoryStream();

        // Act
        await serializer.SerializeAsync(stream, obj, CancellationToken.None);
        stream.Position = 0; // Reset position for reading

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var result = await reader.ReadToEndAsync();

        // Assert
        Assert.Contains("\"Name\":\"Test\"", result);
    }

    [Fact]
    public async Task DeserializeAsync_ValidJson_ShouldReturnObject()
    {
        // Arrange
        var json = "{\"Name\":\"Test\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var serializer = new DefaultJsonSerializer(null);

        // Act
        var result = await serializer.DeserializeAsync<TestClass>(stream, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result?.Name);
    }

    private class TestClass
    {
        public string Name { get; set; } = default!;
    }
}
