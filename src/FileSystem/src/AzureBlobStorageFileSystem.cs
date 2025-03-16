// namespace Femur.FileSystem;

// using System;
// using System.IO;
// using System.Text;
// using System.Threading.Tasks;
// using Azure.Storage.Blobs;
// using Azure.Storage.Blobs.Models;

// public class AzureBlobFileSystem : IFileSystem
// {
//     private readonly BlobContainerClient _containerClient;
//     private readonly string _rootDirectory;

//     public AzureBlobFileSystem(string connectionString, string containerName, string rootDirectory)
//     {
//         _containerClient = new BlobContainerClient(connectionString, containerName);
//         _containerClient.CreateIfNotExists();
//         _rootDirectory = rootDirectory.TrimEnd('/') + "/";
//     }

//     private string NormalizePath(string path)
//     {
//         path = path.Replace("\\", "/");
//         if (path.Contains(".."))
//         {
//             throw new UnauthorizedAccessException("Access outside of root directory is not allowed.");
//         }
//         return _rootDirectory + path.TrimStart('/');
//     }

//     public async Task WriteAsync(string path, Stream content)
//     {
//         string blobPath = NormalizePath(path);
//         BlobClient blobClient = _containerClient.GetBlobClient(blobPath);
//         await blobClient.UploadAsync(content, true);
//     }

//     public async Task<Stream> OpenReadAsync(string path)
//     {
//         string blobPath = NormalizePath(path);
//         BlobClient blobClient = _containerClient.GetBlobClient(blobPath);
//         if (!await blobClient.ExistsAsync())
//         {
//             throw new FileNotFoundException("Blob not found.");
//         }
//         MemoryStream stream = new MemoryStream();
//         await blobClient.DownloadToAsync(stream);
//         stream.Position = 0;
//         return stream;
//     }

//     public async Task<bool> FileExistsAsync(string path)
//     {
//         string blobPath = NormalizePath(path);
//         BlobClient blobClient = _containerClient.GetBlobClient(blobPath);
//         return await blobClient.ExistsAsync();
//     }

//     public async Task DeleteFileAsync(string path)
//     {
//         string blobPath = NormalizePath(path);
//         BlobClient blobClient = _containerClient.GetBlobClient(blobPath);
//         await blobClient.DeleteIfExistsAsync();
//     }

//     public async Task CreateDirectoryAsync(string path)
//     {
//         string blobPath = NormalizePath(path) + "/placeholder.txt";
//         BlobClient blobClient = _containerClient.GetBlobClient(blobPath);
//         using var emptyStream = new MemoryStream(Encoding.UTF8.GetBytes(""));
//         await blobClient.UploadAsync(emptyStream, true);
//     }

//     public async Task<bool> DirectoryExistsAsync(string path)
//     {
//         string prefix = NormalizePath(path);
//         await foreach (BlobItem blobItem in _containerClient.GetBlobsAsync(prefix: prefix))
//         {
//             return true;
//         }
//         return false;
//     }

//     public async Task DeleteDirectoryAsync(string path, bool recursive = true)
//     {
//         string prefix = NormalizePath(path);
//         await foreach (BlobItem blobItem in _containerClient.GetBlobsAsync(prefix: prefix))
//         {
//             BlobClient blobClient = _containerClient.GetBlobClient(blobItem.Name);
//             await blobClient.DeleteIfExistsAsync();
//         }
//     }
// }
