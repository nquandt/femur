
namespace Femur.FileSystem;

public interface IFileSystem : IReadonlyFileSystem
{
    Task WriteAsync(string filePath, Stream data, bool overwrite = true, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task DeleteDirectoryAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default);

    Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
}
