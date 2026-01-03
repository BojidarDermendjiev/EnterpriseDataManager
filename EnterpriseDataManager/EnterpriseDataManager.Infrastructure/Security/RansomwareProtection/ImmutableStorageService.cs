namespace EnterpriseDataManager.Infrastructure.Security.RansomwareProtection;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

public class ImmutableStorageService
{
    private readonly string _storagePath;
    private readonly ILogger<ImmutableStorageService>? _logger;
    private readonly ConcurrentDictionary<string, ImmutableFileRecord> _fileRecords = new();
    private readonly TimeSpan _defaultRetentionPeriod;

    public ImmutableStorageService(
        string storagePath,
        TimeSpan? defaultRetentionPeriod = null,
        ILogger<ImmutableStorageService>? logger = null)
    {
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
        _defaultRetentionPeriod = defaultRetentionPeriod ?? TimeSpan.FromDays(365);
        _logger = logger;

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task<ImmutableFileRecord> StoreImmutableAsync(
        Stream content,
        string originalPath,
        TimeSpan? retentionPeriod = null,
        CancellationToken cancellationToken = default)
    {
        var fileId = Guid.NewGuid().ToString();
        var retention = retentionPeriod ?? _defaultRetentionPeriod;
        var expiresAt = DateTimeOffset.UtcNow.Add(retention);

        // Calculate hash before storing
        using var hashStream = new MemoryStream();
        await content.CopyToAsync(hashStream, cancellationToken);
        hashStream.Position = 0;

        var hash = await CalculateHashAsync(hashStream, cancellationToken);
        hashStream.Position = 0;

        // Store the file
        var storagePath = GetStoragePath(fileId);
        await using var fileStream = new FileStream(storagePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await hashStream.CopyToAsync(fileStream, cancellationToken);

        // Make file read-only
        var fileInfo = new FileInfo(storagePath);
        fileInfo.IsReadOnly = true;

        var record = new ImmutableFileRecord(
            FileId: fileId,
            OriginalPath: originalPath,
            StoragePath: storagePath,
            Hash: hash,
            SizeBytes: fileInfo.Length,
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: expiresAt,
            IsLegalHold: false,
            LegalHoldReason: null);

        _fileRecords[fileId] = record;
        _logger?.LogInformation("Stored immutable file {FileId} from {OriginalPath}, expires at {ExpiresAt}",
            fileId, originalPath, expiresAt);

        return record;
    }

    public async Task<Stream> RetrieveAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (!_fileRecords.TryGetValue(fileId, out var record))
        {
            throw new FileNotFoundException($"Immutable file {fileId} not found");
        }

        if (!File.Exists(record.StoragePath))
        {
            throw new FileNotFoundException($"Immutable file {fileId} storage not found");
        }

        var memoryStream = new MemoryStream();
        await using var fileStream = new FileStream(record.StoragePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public async Task<bool> VerifyIntegrityAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (!_fileRecords.TryGetValue(fileId, out var record))
        {
            return false;
        }

        if (!File.Exists(record.StoragePath))
        {
            return false;
        }

        await using var stream = new FileStream(record.StoragePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var currentHash = await CalculateHashAsync(stream, cancellationToken);

        var isValid = currentHash == record.Hash;

        if (!isValid)
        {
            _logger?.LogWarning("Integrity check failed for immutable file {FileId}. Expected: {Expected}, Got: {Actual}",
                fileId, record.Hash, currentHash);
        }

        return isValid;
    }

    public Task ApplyLegalHoldAsync(string fileId, string reason, CancellationToken cancellationToken = default)
    {
        if (!_fileRecords.TryGetValue(fileId, out var record))
        {
            throw new FileNotFoundException($"Immutable file {fileId} not found");
        }

        var updated = record with
        {
            IsLegalHold = true,
            LegalHoldReason = reason,
            ExpiresAt = DateTimeOffset.MaxValue
        };

        _fileRecords[fileId] = updated;
        _logger?.LogInformation("Applied legal hold to immutable file {FileId}: {Reason}", fileId, reason);

        return Task.CompletedTask;
    }

    public Task ReleaseLegalHoldAsync(string fileId, TimeSpan? newRetention = null, CancellationToken cancellationToken = default)
    {
        if (!_fileRecords.TryGetValue(fileId, out var record))
        {
            throw new FileNotFoundException($"Immutable file {fileId} not found");
        }

        if (!record.IsLegalHold)
        {
            return Task.CompletedTask;
        }

        var newExpiry = DateTimeOffset.UtcNow.Add(newRetention ?? _defaultRetentionPeriod);
        var updated = record with
        {
            IsLegalHold = false,
            LegalHoldReason = null,
            ExpiresAt = newExpiry
        };

        _fileRecords[fileId] = updated;
        _logger?.LogInformation("Released legal hold from immutable file {FileId}, new expiry: {ExpiresAt}",
            fileId, newExpiry);

        return Task.CompletedTask;
    }

    public Task<ImmutableFileRecord?> GetRecordAsync(string fileId, CancellationToken cancellationToken = default)
    {
        _fileRecords.TryGetValue(fileId, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<ImmutableFileRecord>> GetExpiringFilesAsync(
        TimeSpan withinPeriod,
        CancellationToken cancellationToken = default)
    {
        var threshold = DateTimeOffset.UtcNow.Add(withinPeriod);
        var expiring = _fileRecords.Values
            .Where(r => !r.IsLegalHold && r.ExpiresAt <= threshold)
            .OrderBy(r => r.ExpiresAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<ImmutableFileRecord>>(expiring);
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _fileRecords.Values
            .Where(r => !r.IsLegalHold && r.ExpiresAt <= now)
            .ToList();

        var count = 0;
        foreach (var record in expired)
        {
            try
            {
                if (File.Exists(record.StoragePath))
                {
                    var fileInfo = new FileInfo(record.StoragePath);
                    fileInfo.IsReadOnly = false;
                    File.Delete(record.StoragePath);
                }

                _fileRecords.TryRemove(record.FileId, out _);
                count++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to cleanup expired immutable file {FileId}", record.FileId);
            }
        }

        if (count > 0)
        {
            _logger?.LogInformation("Cleaned up {Count} expired immutable files", count);
        }

        return await Task.FromResult(count);
    }

    private string GetStoragePath(string fileId)
    {
        return Path.Combine(_storagePath, $"{fileId}.immutable");
    }

    private static async Task<string> CalculateHashAsync(Stream stream, CancellationToken cancellationToken)
    {
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

public record ImmutableFileRecord(
    string FileId,
    string OriginalPath,
    string StoragePath,
    string Hash,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsLegalHold,
    string? LegalHoldReason);
