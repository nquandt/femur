using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Femur.FileSystem;

public class DefaultFileSystem : IFileSystem
{
    private readonly string _rootDirectory;
    
    public DefaultFileSystem(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(_rootDirectory))
        {
            Directory.CreateDirectory(_rootDirectory);
        }
    }

    private string ResolvePath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, path));
        if (!fullPath.StartsWith(_rootDirectory))
        {
            throw new UnauthorizedAccessException("Access outside of root directory is not allowed.");
        }
        return fullPath;
    }

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(ResolvePath(path)));

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(Directory.Exists(ResolvePath(path)));

    public Task<IEnumerable<string>> GetFilesAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(directoryPath);
        if (!Directory.Exists(resolvedPath))
            throw new DirectoryNotFoundException($"Directory not found: {resolvedPath}");
        
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Task.FromResult<IEnumerable<string>>(Directory.GetFiles(resolvedPath, "*", searchOption));
    }

    public Task<IEnumerable<string>> GetDirectoriesAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(directoryPath);
        if (!Directory.Exists(resolvedPath))
            throw new DirectoryNotFoundException($"Directory not found: {resolvedPath}");
        
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Task.FromResult<IEnumerable<string>>(Directory.GetDirectories(resolvedPath, "*", searchOption));
    }

    public Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(filePath);
        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException("File not found", resolvedPath);
        
        return Task.FromResult<Stream>(File.OpenRead(resolvedPath));
    }

    public async Task WriteAsync(string filePath, Stream data, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(filePath);
        var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
        using var fileStream = new FileStream(resolvedPath, mode, FileAccess.Write, FileShare.None);
        await data.CopyToAsync(fileStream, cancellationToken);
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(filePath);
        if (File.Exists(resolvedPath)) File.Delete(resolvedPath);
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(directoryPath);
        if (Directory.Exists(resolvedPath))
            Directory.Delete(resolvedPath, recursive);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ResolvePath(directoryPath));
        return Task.CompletedTask;
    }
}
