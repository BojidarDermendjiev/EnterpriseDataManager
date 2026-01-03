namespace EnterpriseDataManager.Infrastructure.Storage;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EnterpriseDataManager.Core.Interfaces.Services;
using System.Security.Cryptography;

public class AzureBlobProvider : IStorageProviderAdapter
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _prefix;

    public AzureBlobProvider(BlobContainerClient containerClient, string? prefix = null)
    {
        _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        _prefix = prefix?.TrimEnd('/') ?? string.Empty;
    }

    public AzureBlobProvider(string connectionString, string containerName, string? prefix = null)
    {
        var serviceClient = new BlobServiceClient(connectionString);
        _containerClient = serviceClient.GetBlobContainerClient(containerName);
        _prefix = prefix?.TrimEnd('/') ?? string.Empty;
    }

    private string GetFullBlobName(string path)
    {
        var normalizedPath = path.TrimStart('/');
        return string.IsNullOrEmpty(_prefix)
            ? normalizedPath
            : $"{_prefix}/{normalizedPath}";
    }

    private BlobClient GetBlobClient(string path)
    {
        return _containerClient.GetBlobClient(GetFullBlobName(path));
    }

    public async Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(path);
        var response = await blobClient.DownloadAsync(cancellationToken);

        var memoryStream = new MemoryStream();
        await response.Value.Content.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task WriteAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(path);
        await blobClient.UploadAsync(content, overwrite: true, cancellationToken);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(path);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(path);
        var response = await blobClient.ExistsAsync(cancellationToken);
        return response.Value;
    }

    public async Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(path);
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

        return new FileMetadata(
            Path: path,
            Name: Path.GetFileName(path),
            SizeBytes: properties.Value.ContentLength,
            CreatedAt: properties.Value.CreatedOn,
            ModifiedAt: properties.Value.LastModified,
            Hash: properties.Value.ContentHash != null
                ? Convert.ToHexString(properties.Value.ContentHash).ToLowerInvariant()
                : null,
            IsDirectory: false);
    }

    public async Task<IReadOnlyList<FileMetadata>> ListAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var prefix = GetFullBlobName(path);
        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith('/'))
        {
            prefix += '/';
        }

        var results = new List<FileMetadata>();
        var delimiter = recursive ? null : "/";

        await foreach (var item in _containerClient.GetBlobsByHierarchyAsync(
            delimiter: delimiter,
            prefix: prefix,
            cancellationToken: cancellationToken))
        {
            if (item.IsPrefix)
            {
                var relativePath = string.IsNullOrEmpty(_prefix)
                    ? item.Prefix.TrimEnd('/')
                    : item.Prefix[(_prefix.Length + 1)..].TrimEnd('/');

                results.Add(new FileMetadata(
                    Path: relativePath,
                    Name: Path.GetFileName(relativePath),
                    SizeBytes: 0,
                    CreatedAt: DateTimeOffset.MinValue,
                    ModifiedAt: DateTimeOffset.MinValue,
                    Hash: null,
                    IsDirectory: true));
            }
            else if (item.IsBlob)
            {
                var blob = item.Blob;
                var relativePath = string.IsNullOrEmpty(_prefix)
                    ? blob.Name
                    : blob.Name[(_prefix.Length + 1)..];

                results.Add(new FileMetadata(
                    Path: relativePath,
                    Name: Path.GetFileName(blob.Name),
                    SizeBytes: blob.Properties.ContentLength ?? 0,
                    CreatedAt: blob.Properties.CreatedOn ?? DateTimeOffset.MinValue,
                    ModifiedAt: blob.Properties.LastModified ?? DateTimeOffset.MinValue,
                    Hash: blob.Properties.ContentHash != null
                        ? Convert.ToHexString(blob.Properties.ContentHash).ToLowerInvariant()
                        : null,
                    IsDirectory: false));
            }
        }

        return results;
    }

    public async Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var sourceClient = GetBlobClient(sourcePath);
        var destClient = GetBlobClient(destinationPath);

        var copyOperation = await destClient.StartCopyFromUriAsync(sourceClient.Uri, cancellationToken: cancellationToken);
        await copyOperation.WaitForCompletionAsync(cancellationToken);
    }

    public async Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        await CopyAsync(sourcePath, destinationPath, cancellationToken);
        await DeleteAsync(sourcePath, cancellationToken);
    }

    public async Task<string> CalculateHashAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = await ReadAsync(path, cancellationToken);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<StorageHealthStatus> CheckHealthAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var response = await _containerClient.ExistsAsync(cancellationToken);
            stopwatch.Stop();

            return new StorageHealthStatus(
                ProviderId: providerId,
                IsHealthy: response.Value,
                IsReachable: true,
                Latency: stopwatch.Elapsed,
                ErrorMessage: response.Value ? null : "Container does not exist",
                CheckedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new StorageHealthStatus(
                ProviderId: providerId,
                IsHealthy: false,
                IsReachable: false,
                Latency: stopwatch.Elapsed,
                ErrorMessage: ex.Message,
                CheckedAt: DateTimeOffset.UtcNow);
        }
    }

    public async Task<StorageUsageInfo> GetUsageAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        long usedBytes = 0;
        int fileCount = 0;

        var prefix = string.IsNullOrEmpty(_prefix) ? null : _prefix + "/";

        await foreach (var blob in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            usedBytes += blob.Properties.ContentLength ?? 0;
            fileCount++;
        }

        return new StorageUsageInfo(
            ProviderId: providerId,
            UsedBytes: usedBytes,
            QuotaBytes: null,
            UsagePercentage: null,
            FileCount: fileCount,
            CalculatedAt: DateTimeOffset.UtcNow);
    }
}
