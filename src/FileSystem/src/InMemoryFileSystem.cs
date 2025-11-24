using System.Collections.Concurrent;

namespace Femur.FileSystem;

public class InMemoryFileSystem : IFileSystem
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();
    private readonly HashSet<string> _directories = new();
    private readonly Uri _rootDirectory;
    private readonly string _tempDirectory;

    public InMemoryFileSystem(string tempDirectory)
    {
        this._rootDirectory = new("mem://app.root/z");
        this._tempDirectory = tempDirectory;
        _ = this._directories.Add(this._rootDirectory.AbsoluteUri); // Root directory
    }

    private string ResolvePath(string path)
    {
        var fullPath = new Uri(Path.Join(this._rootDirectory.AbsoluteUri, path)).AbsoluteUri;

        if (!fullPath.StartsWith(this._rootDirectory.AbsoluteUri))
        {
            throw new UnauthorizedAccessException("Access outside of root directory is not allowed.");
        }

        return fullPath;
    }

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(this._files.ContainsKey(this.ResolvePath(path)));

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(this._directories.Contains(this.ResolvePath(path)));

    public Task<IEnumerable<string>> GetFilesAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var resolvedPath = this.ResolvePath(directoryPath);
        var files = this._files.Keys.Where(f => f.StartsWith(resolvedPath)).ToList();
        return Task.FromResult<IEnumerable<string>>(files);
    }

    public Task<IEnumerable<string>> GetDirectoriesAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var resolvedPath = this.ResolvePath(directoryPath);
        var directories = this._directories.Where(d => d.StartsWith(resolvedPath)).ToList();
        return Task.FromResult<IEnumerable<string>>(directories);
    }

    public Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = this.ResolvePath(filePath);
        if (this._files.TryGetValue(resolvedPath, out var data))
        {
            return Task.FromResult<Stream>(new MemoryStream(data));
        }

        throw new FileNotFoundException("File not found", filePath);
    }

    public async Task WriteAsync(string filePath, Stream data, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        var resolvedPath = this.ResolvePath(filePath);
        if (!overwrite && this._files.ContainsKey(resolvedPath))
        {
            throw new IOException("File already exists");
        }

        using var memoryStream = new MemoryStream();
        await data.CopyToAsync(memoryStream, cancellationToken);
        this._files[resolvedPath] = memoryStream.ToArray();
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _ = this._files.TryRemove(this.ResolvePath(filePath), out _);
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var resolvedPath = this.ResolvePath(directoryPath);
        if (!this._directories.Contains(resolvedPath))
        {
            return Task.CompletedTask;
        }

        if (recursive)
        {
            foreach (var file in this._files.Keys.Where(f => f.StartsWith(resolvedPath)).ToList())
            {
                _ = this._files.TryRemove(file, out _);
            }

            _ = this._directories.RemoveWhere(d => d.StartsWith(resolvedPath));
        }
        else if (this._files.Keys.Any(f => f.StartsWith(resolvedPath)))
        {
            throw new IOException("Directory is not empty");
        }

        _ = this._directories.Remove(resolvedPath);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        _ = this._directories.Add(this.ResolvePath(directoryPath));
        return Task.CompletedTask;
    }

    public async Task FlushToDiskAsync()
    {
        _ = Directory.CreateDirectory(this._tempDirectory);
        foreach (var file in this._files)
        {
            var filePath = Path.Join(this._tempDirectory, file.Key.Substring(this._rootDirectory.AbsoluteUri.Length));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllBytesAsync(filePath, file.Value);
        }
    }

    public async Task LoadFromDiskAsync()
    {
        if (!Directory.Exists(this._tempDirectory))
        {
            return;
        }

        foreach (var filePath in Directory.GetFiles(this._tempDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = this.ResolvePath(Path.GetRelativePath(this._tempDirectory, filePath).Replace("\\", "/"));
            this._files[relativePath] = await File.ReadAllBytesAsync(filePath);
            _ = this._directories.Add(Path.GetDirectoryName(relativePath)!);
        }
    }
}
