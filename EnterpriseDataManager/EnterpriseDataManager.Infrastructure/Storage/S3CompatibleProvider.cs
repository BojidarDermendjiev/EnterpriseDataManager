namespace EnterpriseDataManager.Infrastructure.Storage;

using Amazon.S3;
using Amazon.S3.Model;
using EnterpriseDataManager.Core.Interfaces.Services;
using System.Security.Cryptography;

public class S3CompatibleProvider : IStorageProviderAdapter, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _prefix;
    private bool _disposed;

    public S3CompatibleProvider(string accessKey, IAmazonS3 s3Client, string bucketName, string? prefix = null)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        _prefix = prefix?.TrimEnd('/') ?? string.Empty;
    }

    private string GetFullKey(string path)
    {
        var normalizedPath = path.TrimStart('/');
        return string.IsNullOrEmpty(_prefix)
            ? normalizedPath
            : $"{_prefix}/{normalizedPath}";
    }

    public async Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var key = GetFullKey(path);

        var response = await _s3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        }, cancellationToken);

        var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task WriteAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        var key = GetFullKey(path);

        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content
        }, cancellationToken);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var key = GetFullKey(path);

        await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        }, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var key = GetFullKey(path);

        try
        {
            await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            }, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        var key = GetFullKey(path);

        var metadata = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = _bucketName,
            Key = key
        }, cancellationToken);

        return new FileMetadata(
            Path: path,
            Name: Path.GetFileName(path),
            SizeBytes: metadata.ContentLength,
            CreatedAt: metadata.LastModified,
            ModifiedAt: metadata.LastModified,
            Hash: metadata.ETag?.Trim('"'),
            IsDirectory: false);
    }

    public async Task<IReadOnlyList<FileMetadata>> ListAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var prefix = GetFullKey(path);
        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith('/'))
        {
            prefix += '/';
        }

        var results = new List<FileMetadata>();
        string? continuationToken = null;

        do
        {
            var response = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix,
                Delimiter = recursive ? null : "/",
                ContinuationToken = continuationToken
            }, cancellationToken);

            foreach (var obj in response.S3Objects)
            {
                var relativePath = string.IsNullOrEmpty(_prefix)
                    ? obj.Key
                    : obj.Key[(_prefix.Length + 1)..];

                results.Add(new FileMetadata(
                    Path: relativePath,
                    Name: Path.GetFileName(obj.Key),
                    SizeBytes: obj.Size,
                    CreatedAt: obj.LastModified,
                    ModifiedAt: obj.LastModified,
                    Hash: obj.ETag?.Trim('"'),
                    IsDirectory: false));
            }

            foreach (var commonPrefix in response.CommonPrefixes)
            {
                var relativePath = string.IsNullOrEmpty(_prefix)
                    ? commonPrefix.TrimEnd('/')
                    : commonPrefix[(_prefix.Length + 1)..].TrimEnd('/');

                results.Add(new FileMetadata(
                    Path: relativePath,
                    Name: Path.GetFileName(relativePath),
                    SizeBytes: 0,
                    CreatedAt: DateTimeOffset.MinValue,
                    ModifiedAt: DateTimeOffset.MinValue,
                    Hash: null,
                    IsDirectory: true));
            }

            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (continuationToken != null);

        return results;
    }

    public async Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var sourceKey = GetFullKey(sourcePath);
        var destKey = GetFullKey(destinationPath);

        await _s3Client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = _bucketName,
            SourceKey = sourceKey,
            DestinationBucket = _bucketName,
            DestinationKey = destKey
        }, cancellationToken);
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
            await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                MaxKeys = 1
            }, cancellationToken);

            stopwatch.Stop();

            return new StorageHealthStatus(
                ProviderId: providerId,
                IsHealthy: true,
                IsReachable: true,
                Latency: stopwatch.Elapsed,
                ErrorMessage: null,
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
        string? continuationToken = null;

        var prefix = string.IsNullOrEmpty(_prefix) ? null : _prefix + "/";

        do
        {
            var response = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken
            }, cancellationToken);

            foreach (var obj in response.S3Objects)
            {
                usedBytes += obj.Size;
                fileCount++;
            }

            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (continuationToken != null);

        return new StorageUsageInfo(
            ProviderId: providerId,
            UsedBytes: usedBytes,
            QuotaBytes: null,
            UsagePercentage: null,
            FileCount: fileCount,
            CalculatedAt: DateTimeOffset.UtcNow);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _s3Client.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
