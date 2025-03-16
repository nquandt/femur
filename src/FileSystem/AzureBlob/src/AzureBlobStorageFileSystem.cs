

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Femur.FileSystem.AzureBlob;

public class AzureBlobFileSystem : IFileSystem
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _rootDirectory;

    public AzureBlobFileSystem(BlobContainerClient blobContainerClient, string rootDirectory)
    {   
        _containerClient = blobContainerClient;     
        _containerClient.CreateIfNotExists();
        _rootDirectory = rootDirectory.TrimEnd('/') + "/";
    }

    public Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteDirectoryAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<string>> GetDirectoriesAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<string>> GetFilesAsync(string directoryPath, bool recursive = false, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task WriteAsync(string filePath, Stream data, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
