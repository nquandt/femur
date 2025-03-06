using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace Femur.Serialization;

internal class DefaultJsonSerializer : IAsyncSerializer
{
    private readonly JsonSerializerOptions _options;
    public DefaultJsonSerializer(JsonSerializerOptions? jsonSerializerOptions)
    {
        _options = jsonSerializerOptions ?? JsonSerializerOptions.Default;
    }

    public string[] ContentTypes => [Application.Json];

    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class
    {
        return await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken).ConfigureAwait(false);
    }

    public async Task SerializeAsync<T>(Stream stream, T obj, CancellationToken cancellationToken = default) where T : class
    {
        await JsonSerializer.SerializeAsync<T>(stream, obj, _options, cancellationToken).ConfigureAwait(false);
    }
}