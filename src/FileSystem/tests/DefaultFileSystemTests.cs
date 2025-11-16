

using System.Text;

namespace Femur.FileSystem.Tests;

public class DefaultFileSystemTests
{
    private readonly IFileSystem _fileSystem;
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "StandardFileSystemTests");

    public DefaultFileSystemTests()
    {
        if (Directory.Exists(_rootDirectory))
        {
            try
            {
                // Remove read-only attributes and delete recursively
                var directoryInfo = new DirectoryInfo(_rootDirectory);
                directoryInfo.Attributes = FileAttributes.Normal;
                foreach (var file in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes = FileAttributes.Normal;
                }
                Directory.Delete(_rootDirectory, true);
            }
            catch (IOException)
            {
                // If deletion fails, try to remove individual files
                try
                {
                    var directoryInfo = new DirectoryInfo(_rootDirectory);
                    foreach (var file in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
                    {
                        file.Attributes = FileAttributes.Normal;
                        file.Delete();
                    }
                    foreach (var dir in directoryInfo.GetDirectories("*", SearchOption.AllDirectories))
                    {
                        dir.Attributes = FileAttributes.Normal;
                        dir.Delete(true);
                    }
                    directoryInfo.Delete(true);
                }
                catch
                {
                    // If all else fails, just use the existing directory
                }
            }
        }
        Directory.CreateDirectory(_rootDirectory);
        _fileSystem = new DefaultFileSystem(_rootDirectory);
    }

    [Fact]
    public async Task WriteAndReadFile_ShouldSucceed()
    {
        string filePath = "test.txt";
        byte[] data = Encoding.UTF8.GetBytes("Hello Standard FS");
        using var stream = new MemoryStream(data);

        await _fileSystem.WriteAsync(filePath, stream);
        using var readStream = await _fileSystem.OpenReadAsync(filePath);
        using var reader = new StreamReader(readStream);
        string content = await reader.ReadToEndAsync();
        
        Assert.Equal("Hello Standard FS", content);
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
}
