namespace EnterpriseDataManager.Infrastructure.Storage;

using EnterpriseDataManager.Core.Interfaces.Services;
using System.Security.Cryptography;

public class LocalFilesystemProvider : IStorageProviderAdapter
{
    private readonly string _rootPath;

    public LocalFilesystemProvider(string rootPath)
    {
        _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));

        if (!Directory.Exists(_rootPath))
        {
            Directory.CreateDirectory(_rootPath);
        }
    }

    private string GetFullPath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, path.TrimStart('/', '\\')));

        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access to path outside root directory is not allowed.");
        }

        return fullPath;
    }

    public async Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        var memoryStream = new MemoryStream();
        await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task WriteAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        else if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        return Task.FromResult(File.Exists(fullPath) || Directory.Exists(fullPath));
    }

    public Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        if (File.Exists(fullPath))
        {
            var fileInfo = new FileInfo(fullPath);
            return Task.FromResult(new FileMetadata(
                Path: path,
                Name: fileInfo.Name,
                SizeBytes: fileInfo.Length,
                CreatedAt: fileInfo.CreationTimeUtc,
                ModifiedAt: fileInfo.LastWriteTimeUtc,
                Hash: null,
                IsDirectory: false));
        }

        if (Directory.Exists(fullPath))
        {
            var dirInfo = new DirectoryInfo(fullPath);
            return Task.FromResult(new FileMetadata(
                Path: path,
                Name: dirInfo.Name,
                SizeBytes: 0,
                CreatedAt: dirInfo.CreationTimeUtc,
                ModifiedAt: dirInfo.LastWriteTimeUtc,
                Hash: null,
                IsDirectory: true));
        }

        throw new FileNotFoundException($"Path not found: {path}");
    }

    public Task<IReadOnlyList<FileMetadata>> ListAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult<IReadOnlyList<FileMetadata>>(Array.Empty<FileMetadata>());
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var results = new List<FileMetadata>();

        foreach (var dir in Directory.EnumerateDirectories(fullPath, "*", searchOption))
        {
            var dirInfo = new DirectoryInfo(dir);
            var relativePath = Path.GetRelativePath(_rootPath, dir).Replace('\\', '/');
            results.Add(new FileMetadata(
                Path: relativePath,
                Name: dirInfo.Name,
                SizeBytes: 0,
                CreatedAt: dirInfo.CreationTimeUtc,
                ModifiedAt: dirInfo.LastWriteTimeUtc,
                Hash: null,
                IsDirectory: true));
        }

        foreach (var file in Directory.EnumerateFiles(fullPath, "*", searchOption))
        {
            var fileInfo = new FileInfo(file);
            var relativePath = Path.GetRelativePath(_rootPath, file).Replace('\\', '/');
            results.Add(new FileMetadata(
                Path: relativePath,
                Name: fileInfo.Name,
                SizeBytes: fileInfo.Length,
                CreatedAt: fileInfo.CreationTimeUtc,
                ModifiedAt: fileInfo.LastWriteTimeUtc,
                Hash: null,
                IsDirectory: false));
        }

        return Task.FromResult<IReadOnlyList<FileMetadata>>(results);
    }

    public async Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var sourceFullPath = GetFullPath(sourcePath);
        var destFullPath = GetFullPath(destinationPath);

        var destDir = Path.GetDirectoryName(destFullPath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        await using var sourceStream = new FileStream(sourceFullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await using var destStream = new FileStream(destFullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await sourceStream.CopyToAsync(destStream, cancellationToken);
    }

    public async Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        await CopyAsync(sourcePath, destinationPath, cancellationToken);
        await DeleteAsync(sourcePath, cancellationToken);
    }

    public async Task<string> CalculateHashAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public Task<StorageHealthStatus> CheckHealthAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var isReachable = Directory.Exists(_rootPath);
            stopwatch.Stop();

            return Task.FromResult(new StorageHealthStatus(
                ProviderId: providerId,
                IsHealthy: isReachable,
                IsReachable: isReachable,
                Latency: stopwatch.Elapsed,
                ErrorMessage: isReachable ? null : "Root path does not exist",
                CheckedAt: DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(new StorageHealthStatus(
                ProviderId: providerId,
                IsHealthy: false,
                IsReachable: false,
                Latency: stopwatch.Elapsed,
                ErrorMessage: ex.Message,
                CheckedAt: DateTimeOffset.UtcNow));
        }
    }

    public Task<StorageUsageInfo> GetUsageAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        long usedBytes = 0;
        int fileCount = 0;

        if (Directory.Exists(_rootPath))
        {
            foreach (var file in Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                usedBytes += info.Length;
                fileCount++;
            }
        }

        return Task.FromResult(new StorageUsageInfo(
            ProviderId: providerId,
            UsedBytes: usedBytes,
            QuotaBytes: null,
            UsagePercentage: null,
            FileCount: fileCount,
            CalculatedAt: DateTimeOffset.UtcNow));
    }
}
