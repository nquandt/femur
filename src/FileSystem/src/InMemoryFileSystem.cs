using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Femur.FileSystem;

public class InMemoryFileSystem : IFileSystem
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();
    private readonly HashSet<string> _directories = new();
    private readonly Uri _rootDirectory;
    private readonly string _tempDirectory;

    public InMemoryFileSystem(string tempDirectory)
    {
        _rootDirectory = new("mem://app.root/z");
        _tempDirectory = tempDirectory;
        _directories.Add(_rootDirectory.AbsoluteUri); // Root directory
    }

    private string ResolvePath(string path)
    {
        var fullPath = new Uri(Path.Join(_rootDirectory.AbsoluteUri, path)).AbsoluteUri;

        if (!fullPath.StartsWith(_rootDirectory.AbsoluteUri))
        {
            throw new UnauthorizedAccessException("Access outside of root directory is not allowed.");
        }
        return fullPath;
    }

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(_files.ContainsKey(ResolvePath(path)));

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(_directories.Contains(ResolvePath(path)));

    public Task<IEnumerable<string>> GetFilesAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(directoryPath);
        var files = _files.Keys.Where(f => f.StartsWith(resolvedPath)).ToList();
        return Task.FromResult<IEnumerable<string>>(files);
    }

    public Task<IEnumerable<string>> GetDirectoriesAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(directoryPath);
        var directories = _directories.Where(d => d.StartsWith(resolvedPath)).ToList();
        return Task.FromResult<IEnumerable<string>>(directories);
    }

    public Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(filePath);
        if (_files.TryGetValue(resolvedPath, out var data))
        {
            return Task.FromResult<Stream>(new MemoryStream(data));
        }
        throw new FileNotFoundException("File not found", filePath);
    }

    public async Task WriteAsync(string filePath, Stream data, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(filePath);
        if (!overwrite && _files.ContainsKey(resolvedPath))
            throw new IOException("File already exists");

        using var memoryStream = new MemoryStream();
        await data.CopyToAsync(memoryStream, cancellationToken);
        _files[resolvedPath] = memoryStream.ToArray();
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _files.TryRemove(ResolvePath(filePath), out _);
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(directoryPath);
        if (!_directories.Contains(resolvedPath)) return Task.CompletedTask;

        if (recursive)
        {
            foreach (var file in _files.Keys.Where(f => f.StartsWith(resolvedPath)).ToList())
            {
                _files.TryRemove(file, out _);
            }
            _directories.RemoveWhere(d => d.StartsWith(resolvedPath));
        }
        else if (_files.Keys.Any(f => f.StartsWith(resolvedPath)))
        {
            throw new IOException("Directory is not empty");
        }
        _directories.Remove(resolvedPath);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        _directories.Add(ResolvePath(directoryPath));
        return Task.CompletedTask;
    }

    public async Task FlushToDiskAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        foreach (var file in _files)
        {
            var filePath = Path.Join(_tempDirectory, file.Key.Substring(_rootDirectory.AbsoluteUri.Length));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllBytesAsync(filePath, file.Value);
        }
    }

    public async Task LoadFromDiskAsync()
    {
        if (!Directory.Exists(_tempDirectory)) return;

        foreach (var filePath in Directory.GetFiles(_tempDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = ResolvePath(Path.GetRelativePath(_tempDirectory, filePath).Replace("\\", "/"));
            _files[relativePath] = await File.ReadAllBytesAsync(filePath);
            _directories.Add(Path.GetDirectoryName(relativePath)!);
        }
    }
}
