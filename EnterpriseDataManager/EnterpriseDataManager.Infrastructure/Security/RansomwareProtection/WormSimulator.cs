namespace EnterpriseDataManager.Infrastructure.Security.RansomwareProtection;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

public class WormSimulator
{
    private readonly ImmutableStorageService _immutableStorage;
    private readonly ILogger<WormSimulator>? _logger;
    private readonly ConcurrentDictionary<string, WormRecord> _wormRecords = new();
    private bool _isWormEnabled = true;

    public WormSimulator(ImmutableStorageService immutableStorage, ILogger<WormSimulator>? logger = null)
    {
        _immutableStorage = immutableStorage ?? throw new ArgumentNullException(nameof(immutableStorage));
        _logger = logger;
    }

    public bool IsWormEnabled => _isWormEnabled;

    public async Task<WormRecord> WriteOnceAsync(
        Stream content,
        string path,
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default)
    {
        if (!_isWormEnabled)
        {
            throw new InvalidOperationException("WORM mode is disabled");
        }

        // Check if path already has a WORM record
        if (_wormRecords.TryGetValue(path, out var existingRecord))
        {
            if (existingRecord.State == WormState.Committed)
            {
                throw new InvalidOperationException($"Path {path} already has a committed WORM record");
            }
        }

        // Store in immutable storage
        var immutableRecord = await _immutableStorage.StoreImmutableAsync(
            content, path, retentionPeriod, cancellationToken);

        var wormRecord = new WormRecord(
            RecordId: Guid.NewGuid().ToString(),
            Path: path,
            ImmutableFileId: immutableRecord.FileId,
            State: WormState.Committed,
            RetentionExpiresAt: immutableRecord.ExpiresAt,
            CreatedAt: DateTimeOffset.UtcNow,
            CommittedAt: DateTimeOffset.UtcNow,
            IsLegalHold: false);

        _wormRecords[path] = wormRecord;
        _logger?.LogInformation("Created WORM record {RecordId} for path {Path}, retention until {ExpiresAt}",
            wormRecord.RecordId, path, wormRecord.RetentionExpiresAt);

        return wormRecord;
    }

    public async Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_wormRecords.TryGetValue(path, out var record))
        {
            throw new FileNotFoundException($"No WORM record found for path {path}");
        }

        if (record.State != WormState.Committed)
        {
            throw new InvalidOperationException($"WORM record for {path} is not in committed state");
        }

        return await _immutableStorage.RetrieveAsync(record.ImmutableFileId, cancellationToken);
    }

    public Task<bool> CanDeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_wormRecords.TryGetValue(path, out var record))
        {
            return Task.FromResult(false);
        }

        // Can only delete if:
        // 1. Not under legal hold
        // 2. Retention period has expired
        var canDelete = !record.IsLegalHold && record.RetentionExpiresAt <= DateTimeOffset.UtcNow;

        return Task.FromResult(canDelete);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!await CanDeleteAsync(path, cancellationToken))
        {
            throw new InvalidOperationException($"Cannot delete WORM record for {path}: retention not expired or under legal hold");
        }

        if (_wormRecords.TryRemove(path, out var record))
        {
            _logger?.LogInformation("Deleted WORM record {RecordId} for path {Path}", record.RecordId, path);
        }
    }

    public async Task ExtendRetentionAsync(string path, TimeSpan additionalPeriod, CancellationToken cancellationToken = default)
    {
        if (!_wormRecords.TryGetValue(path, out var record))
        {
            throw new FileNotFoundException($"No WORM record found for path {path}");
        }

        // Retention can only be extended, never shortened (WORM compliance)
        var newExpiry = record.RetentionExpiresAt.Add(additionalPeriod);
        if (newExpiry <= record.RetentionExpiresAt)
        {
            throw new InvalidOperationException("Cannot shorten retention period in WORM mode");
        }

        var updated = record with { RetentionExpiresAt = newExpiry };
        _wormRecords[path] = updated;

        _logger?.LogInformation("Extended retention for WORM record {RecordId} to {NewExpiry}",
            record.RecordId, newExpiry);

        await Task.CompletedTask;
    }

    public Task ApplyLegalHoldAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_wormRecords.TryGetValue(path, out var record))
        {
            throw new FileNotFoundException($"No WORM record found for path {path}");
        }

        var updated = record with { IsLegalHold = true };
        _wormRecords[path] = updated;

        _logger?.LogInformation("Applied legal hold to WORM record {RecordId}", record.RecordId);

        return Task.CompletedTask;
    }

    public Task ReleaseLegalHoldAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_wormRecords.TryGetValue(path, out var record))
        {
            throw new FileNotFoundException($"No WORM record found for path {path}");
        }

        var updated = record with { IsLegalHold = false };
        _wormRecords[path] = updated;

        _logger?.LogInformation("Released legal hold from WORM record {RecordId}", record.RecordId);

        return Task.CompletedTask;
    }

    public Task<WormRecord?> GetRecordAsync(string path, CancellationToken cancellationToken = default)
    {
        _wormRecords.TryGetValue(path, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<WormRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default)
    {
        var records = _wormRecords.Values.ToList();
        return Task.FromResult<IReadOnlyList<WormRecord>>(records);
    }

    public async Task<bool> VerifyIntegrityAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_wormRecords.TryGetValue(path, out var record))
        {
            return false;
        }

        return await _immutableStorage.VerifyIntegrityAsync(record.ImmutableFileId, cancellationToken);
    }

    public void EnableWorm()
    {
        _isWormEnabled = true;
        _logger?.LogInformation("WORM mode enabled");
    }

    public void DisableWorm()
    {
        _isWormEnabled = false;
        _logger?.LogWarning("WORM mode disabled - this may violate compliance requirements");
    }
}

public record WormRecord(
    string RecordId,
    string Path,
    string ImmutableFileId,
    WormState State,
    DateTimeOffset RetentionExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CommittedAt,
    bool IsLegalHold);

public enum WormState
{
    Pending,
    Committed,
    Expired,
    Deleted
}
