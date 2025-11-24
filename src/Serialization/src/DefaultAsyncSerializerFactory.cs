using System.Diagnostics.CodeAnalysis;

namespace Femur.Serialization;

internal class DefaultAsyncSerializerFactory : IAsyncSerializerFactory
{
    private readonly Dictionary<string, IAsyncSerializer> _serializers = new Dictionary<string, IAsyncSerializer>();
    public DefaultAsyncSerializerFactory(IEnumerable<IAsyncSerializer> asyncSerializers)
    {
        foreach (var serializer in asyncSerializers)
        {
            foreach (var contentType in serializer.ContentTypes)
            {
                this._serializers[contentType] = serializer;
            }
        }
    }

    public async Task<T?> DeserializeAsync<T>(Stream stream, string contentType, CancellationToken cancellationToken = default) where T : class
    {
        var serializer = this.GetSerializerOrThrow(contentType);

        return await serializer.DeserializeAsync<T>(stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task SerializeAsync<T>(Stream stream, T obj, string contentType, CancellationToken cancellationToken = default) where T : class
    {
        var serializer = this.GetSerializerOrThrow(contentType);

        await serializer.SerializeAsync(stream, obj, cancellationToken).ConfigureAwait(false);
    }

    public bool SupportsContentType(string contentType)
    {
        return this._serializers.ContainsKey(contentType);
    }

    public bool TryGetSerializer(string contentType, [NotNullWhen(true)] out IAsyncSerializer? serializer)
    {
        return this._serializers.TryGetValue(contentType, out serializer);
    }


    private IAsyncSerializer GetSerializerOrThrow(string contentType)
    {
        if (!this.TryGetSerializer(contentType, out var serializer))
        {
            throw new NotSupportedException();
        }

        return serializer;
    }
}