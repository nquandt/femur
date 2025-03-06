using System.Diagnostics.CodeAnalysis;

namespace Femur.Serialization;

public interface IAsyncSerializerFactory
{
    bool SupportsContentType(string contentType);
    bool TryGetSerializer(string contentType, [NotNullWhen(true)] out IAsyncSerializer? serializer);
    Task SerializeAsync<T>(Stream stream, T obj, string contentType, CancellationToken cancellationToken = default) where T : class;
    Task<T?> DeserializeAsync<T>(Stream stream, string contentType, CancellationToken cancellationToken = default) where T : class;
}
