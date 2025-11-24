
namespace Femur.FileSystem;

public interface IReadonlyFileSystem
{
    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
    Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetFilesAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetDirectoriesAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default);
    // Task WriteAsync(string filePath, Stream data, bool overwrite = true, CancellationToken cancellationToken = default);

    // Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);
    // Task DeleteDirectoryAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default);

    // Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
}
