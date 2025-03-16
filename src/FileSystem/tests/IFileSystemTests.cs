using System.Text;

namespace Femur.FileSystem.Tests;

public class IFileSystemTests
{
    private readonly IFileSystem _fileSystem;
    private readonly string _tempDirectory = "./temp";

    public IFileSystemTests()
    {
        _fileSystem = new InMemoryFileSystem(_tempDirectory);
    }

    [Fact]
    public async Task WriteAndReadFile_ShouldSucceed()
    {
        string filePath = "test.txt";
        byte[] data = Encoding.UTF8.GetBytes("Hello World");
        using var stream = new MemoryStream(data);

        await _fileSystem.WriteAsync(filePath, stream);
        using var readStream = await _fileSystem.OpenReadAsync(filePath);
        using var reader = new StreamReader(readStream);
        string content = await reader.ReadToEndAsync();

        Assert.Equal("Hello World", content);
    }

    [Fact]
    public async Task FileExists_ShouldReturnTrueAfterWrite()
    {
        string filePath = "exists.txt";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));

        await _fileSystem.WriteAsync(filePath, stream);
        bool exists = await _fileSystem.FileExistsAsync(filePath);

        Assert.True(exists);
    }

    [Fact]
    public async Task CannotAccessOutsideRoot()
    {
        string outsidePath = "../outside.txt";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _fileSystem.WriteAsync(outsidePath, stream));
        Assert.Equal("Access outside of root directory is not allowed.", exception.Message);
    }

    [Fact]
    public async Task CreateAndCheckDirectoryExists()
    {
        string dirPath = "newDir";
        await _fileSystem.CreateDirectoryAsync(dirPath);
        bool exists = await _fileSystem.DirectoryExistsAsync(dirPath);

        Assert.True(exists);
    }

    [Fact]
    public async Task DeleteFile_ShouldRemoveIt()
    {
        string filePath = "delete.txt";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("delete me"));
        await _fileSystem.WriteAsync(filePath, stream);

        await _fileSystem.DeleteFileAsync(filePath);
        bool exists = await _fileSystem.FileExistsAsync(filePath);

        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteDirectory_ShouldRemoveItIfEmpty()
    {
        string dirPath = "emptyDir";
        await _fileSystem.CreateDirectoryAsync(dirPath);
        await _fileSystem.DeleteDirectoryAsync(dirPath);

        bool exists = await _fileSystem.DirectoryExistsAsync(dirPath);
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteNonEmptyDirectory_ShouldThrowException()
    {
        string dirPath = "nonEmptyDir";
        string filePath = "nonEmptyDir/file.txt";
        await _fileSystem.CreateDirectoryAsync(dirPath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        await _fileSystem.WriteAsync(filePath, stream);

        await Assert.ThrowsAsync<IOException>(() => _fileSystem.DeleteDirectoryAsync(dirPath, false));
    }

    [Fact]
    public async Task FlushToDiskAndLoadFromDisk_ShouldPersistData()
    {
        string filePath = "persisted.txt";
        byte[] data = Encoding.UTF8.GetBytes("Persisted Data");
        using var stream = new MemoryStream(data);

        await _fileSystem.WriteAsync(filePath, stream);
        await (_fileSystem as InMemoryFileSystem)!.FlushToDiskAsync();

        var newFileSystem = new InMemoryFileSystem(_tempDirectory);
        await newFileSystem.LoadFromDiskAsync();

        bool exists = await newFileSystem.FileExistsAsync(filePath);
        Assert.True(exists);
    }
}
