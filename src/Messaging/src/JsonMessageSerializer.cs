using System.Text.Json;

namespace Femur.Messaging;

/// <summary>
/// JSON implementation of IMessageSerializer using System.Text.Json.
/// This is the default serializer used when no custom serializer is provided.
/// </summary>
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Creates a new JSON serializer with default options (camelCase, case-insensitive).
    /// </summary>
    public JsonMessageSerializer() : this(CreateDefaultOptions())
    {
    }

    /// <summary>
    /// Creates a new JSON serializer with custom options.
    /// </summary>
    public JsonMessageSerializer(JsonSerializerOptions options)
    {
        this._options = options;
    }

    public ReadOnlyMemory<byte> Serialize<T>(T message) where T : class
    {
        var json = JsonSerializer.Serialize(message, this._options);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class
    {
        return JsonSerializer.Deserialize<T>(data.Span, this._options)
            ?? throw new InvalidOperationException($"Deserialized message of type {typeof(T).Name} was null");
    }

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
