namespace Femur.Serialization;

public interface IAsyncSerializer
{
    string[] ContentTypes { get; }
    Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class;
    Task SerializeAsync<T>(Stream stream, T obj, CancellationToken cancellationToken = default) where T : class;
}