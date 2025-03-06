// ```test/Serialization.Tests/DefaultAsyncSerializerFactoryTests.cs
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Femur.Serialization.Tests;

public class DefaultAsyncSerializerFactoryTests
{
    [Fact]
    public void SupportsContentType_ValidContentType_ReturnsTrue()
    {
        // Arrange
        var serializer = new DefaultJsonSerializer(null);
        var factory = new DefaultAsyncSerializerFactory(new[] { serializer });

        // Act
        var supportsJson = factory.SupportsContentType("application/json");

        // Assert
        Assert.True(supportsJson);
    }

    [Fact]
    public void SupportsContentType_InvalidContentType_ReturnsFalse()
    {
        // Arrange
        var serializer = new DefaultJsonSerializer(null);
        var factory = new DefaultAsyncSerializerFactory(new[] { serializer });

        // Act
        var supportsText = factory.SupportsContentType("text/plain");

        // Assert
        Assert.False(supportsText);
    }

    [Fact]
    public async Task DeserializeAsync_ValidJson_ReturnsObject()
    {
        // Arrange
        var json = "{\"Name\":\"Test\"}";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        var serializer = new DefaultJsonSerializer(null);
        var factory = new DefaultAsyncSerializerFactory(new[] { serializer });

        // Act
        var result = await factory.DeserializeAsync<TestClass>(stream, "application/json");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result?.Name);
    }

    [Fact]
    public async Task SerializeAsync_ValidObject_SerializesToJson()
    {
        // Arrange
        var obj = new TestClass { Name = "Test" };
        using var stream = new MemoryStream();

        var serializer = new DefaultJsonSerializer(null);
        var factory = new DefaultAsyncSerializerFactory(new[] { serializer });

        // Act
        await factory.SerializeAsync(stream, obj, "application/json");
        stream.Position = 0; // Reset stream position for reading

        using var reader = new StreamReader(stream);
        var result = await reader.ReadToEndAsync();

        // Assert
        Assert.Contains("\"Name\":\"Test\"", result);
    }

    private class TestClass
    {
        public string Name { get; set; } = default!;
    }
}
