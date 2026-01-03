namespace EnterpriseDataManager.Infrastructure.BackgroundJobs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

public class RetentionPolicyEnforcerOptions
{
    public const string SectionName = "RetentionPolicyEnforcer";

    public bool Enabled { get; set; } = true;
    public TimeSpan EnforcementInterval { get; set; } = TimeSpan.FromHours(1);
    public int BatchSize { get; set; } = 100;
    public bool EnableSoftDelete { get; set; } = true;
    public TimeSpan SoftDeleteGracePeriod { get; set; } = TimeSpan.FromDays(30);
    public List<RetentionPolicyConfiguration> Policies { get; set; } = new();
}

public class RetentionPolicyConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan RetentionPeriod { get; set; }
    public RetentionAction Action { get; set; } = RetentionAction.Delete;
    public List<string> TargetPaths { get; set; } = new();
    public List<string> FilePatterns { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
}

public class RetentionPolicyEnforcer : BackgroundService, IRetentionPolicyEnforcer
{
    private readonly RetentionPolicyEnforcerOptions _options;
    private readonly ILogger<RetentionPolicyEnforcer>? _logger;
    private readonly ConcurrentDictionary<string, RetentionPolicy> _policies = new();
    private readonly ConcurrentQueue<RetentionEnforcementResult> _enforcementHistory = new();
    private readonly ConcurrentDictionary<string, RetentionHold> _holds = new();

    public RetentionPolicyEnforcer(
        IOptions<RetentionPolicyEnforcerOptions> options,
        ILogger<RetentionPolicyEnforcer>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        InitializePolicies();
    }

    public Task<string> CreatePolicyAsync(
        string name,
        string description,
        TimeSpan retentionPeriod,
        RetentionAction action,
        IEnumerable<string>? targetPaths = null,
        IEnumerable<string>? filePatterns = null,
        CancellationToken cancellationToken = default)
    {
        var policyId = Guid.NewGuid().ToString();

        var policy = new RetentionPolicy(
            PolicyId: policyId,
            Name: name,
            Description: description,
            RetentionPeriod: retentionPeriod,
            Action: action,
            TargetPaths: targetPaths?.ToList() ?? new List<string>(),
            FilePatterns: filePatterns?.ToList() ?? new List<string>(),
            IsEnabled: true,
            CreatedAt: DateTimeOffset.UtcNow,
            LastEnforcedAt: null);

        _policies[policyId] = policy;

        _logger?.LogInformation("Created retention policy {PolicyName} ({PolicyId}) with retention period {Period}",
            name, policyId, retentionPeriod);

        return Task.FromResult(policyId);
    }

    public Task<bool> UpdatePolicyAsync(
        string policyId,
        string? name = null,
        string? description = null,
        TimeSpan? retentionPeriod = null,
        RetentionAction? action = null,
        CancellationToken cancellationToken = default)
    {
        if (!_policies.TryGetValue(policyId, out var policy))
        {
            return Task.FromResult(false);
        }

        var updated = policy with
        {
            Name = name ?? policy.Name,
            Description = description ?? policy.Description,
            RetentionPeriod = retentionPeriod ?? policy.RetentionPeriod,
            Action = action ?? policy.Action
        };

        _policies[policyId] = updated;

        _logger?.LogInformation("Updated retention policy {PolicyName} ({PolicyId})", updated.Name, policyId);

        return Task.FromResult(true);
    }

    public Task<bool> DeletePolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        var removed = _policies.TryRemove(policyId, out var policy);

        if (removed)
        {
            _logger?.LogInformation("Deleted retention policy {PolicyName} ({PolicyId})", policy!.Name, policyId);
        }

        return Task.FromResult(removed);
    }

    public Task<bool> EnablePolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        if (_policies.TryGetValue(policyId, out var policy))
        {
            _policies[policyId] = policy with { IsEnabled = true };
            _logger?.LogInformation("Enabled retention policy {PolicyName} ({PolicyId})", policy.Name, policyId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> DisablePolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        if (_policies.TryGetValue(policyId, out var policy))
        {
            _policies[policyId] = policy with { IsEnabled = false };
            _logger?.LogInformation("Disabled retention policy {PolicyName} ({PolicyId})", policy.Name, policyId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<RetentionPolicy?> GetPolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        _policies.TryGetValue(policyId, out var policy);
        return Task.FromResult(policy);
    }

    public Task<IReadOnlyList<RetentionPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var policies = _policies.Values.ToList();
        return Task.FromResult<IReadOnlyList<RetentionPolicy>>(policies);
    }

    public Task<string> CreateHoldAsync(
        string name,
        string reason,
        IEnumerable<string> targetPaths,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var holdId = Guid.NewGuid().ToString();

        var hold = new RetentionHold(
            HoldId: holdId,
            Name: name,
            Reason: reason,
            TargetPaths: targetPaths.ToList(),
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: expiresAt,
            IsActive: true);

        _holds[holdId] = hold;

        _logger?.LogInformation("Created retention hold {HoldName} ({HoldId}) for {PathCount} paths",
            name, holdId, hold.TargetPaths.Count);

        return Task.FromResult(holdId);
    }

    public Task<bool> ReleaseHoldAsync(string holdId, CancellationToken cancellationToken = default)
    {
        if (_holds.TryGetValue(holdId, out var hold))
        {
            _holds[holdId] = hold with { IsActive = false };
            _logger?.LogInformation("Released retention hold {HoldName} ({HoldId})", hold.Name, holdId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<RetentionHold>> GetActiveHoldsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var activeHolds = _holds.Values
            .Where(h => h.IsActive && (!h.ExpiresAt.HasValue || h.ExpiresAt.Value > now))
            .ToList();

        return Task.FromResult<IReadOnlyList<RetentionHold>>(activeHolds);
    }

    public Task<bool> IsPathOnHoldAsync(string path, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var isOnHold = _holds.Values
            .Where(h => h.IsActive && (!h.ExpiresAt.HasValue || h.ExpiresAt.Value > now))
            .Any(h => h.TargetPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

        return Task.FromResult(isOnHold);
    }

    public async Task<RetentionEnforcementResult> EnforcePoliciesAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var itemsEvaluated = 0;
        var itemsDeleted = 0;
        var itemsArchived = 0;
        var itemsSkipped = 0;
        var errors = new List<string>();

        _logger?.LogInformation("Starting retention policy enforcement");

        foreach (var policy in _policies.Values.Where(p => p.IsEnabled))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var result = await EnforcePolicyAsync(policy, cancellationToken);

                itemsEvaluated += result.ItemsEvaluated;
                itemsDeleted += result.ItemsDeleted;
                itemsArchived += result.ItemsArchived;
                itemsSkipped += result.ItemsSkipped;
                errors.AddRange(result.Errors);

                // Update last enforced timestamp
                _policies[policy.PolicyId] = policy with { LastEnforcedAt = DateTimeOffset.UtcNow };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error enforcing policy {PolicyName} ({PolicyId})", policy.Name, policy.PolicyId);
                errors.Add($"Policy {policy.Name}: {ex.Message}");
            }
        }

        var enforcementResult = new RetentionEnforcementResult(
            EnforcementId: Guid.NewGuid().ToString(),
            StartedAt: startTime,
            CompletedAt: DateTimeOffset.UtcNow,
            PoliciesEnforced: _policies.Values.Count(p => p.IsEnabled),
            ItemsEvaluated: itemsEvaluated,
            ItemsDeleted: itemsDeleted,
            ItemsArchived: itemsArchived,
            ItemsSkipped: itemsSkipped,
            Errors: errors);

        _enforcementHistory.Enqueue(enforcementResult);

        while (_enforcementHistory.Count > 100)
        {
            _enforcementHistory.TryDequeue(out _);
        }

        _logger?.LogInformation("Retention enforcement completed. Evaluated: {Evaluated}, Deleted: {Deleted}, Archived: {Archived}, Skipped: {Skipped}",
            itemsEvaluated, itemsDeleted, itemsArchived, itemsSkipped);

        return enforcementResult;
    }

    public Task<IReadOnlyList<RetentionEnforcementResult>> GetEnforcementHistoryAsync(
        int count = 50,
        CancellationToken cancellationToken = default)
    {
        var history = _enforcementHistory
            .OrderByDescending(r => r.StartedAt)
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<RetentionEnforcementResult>>(history);
    }

    public async Task<RetentionPreviewResult> PreviewEnforcementAsync(
        string policyId,
        CancellationToken cancellationToken = default)
    {
        if (!_policies.TryGetValue(policyId, out var policy))
        {
            throw new KeyNotFoundException($"Policy {policyId} not found");
        }

        var affectedItems = await GetAffectedItemsAsync(policy, cancellationToken);

        return new RetentionPreviewResult(
            PolicyId: policyId,
            PolicyName: policy.Name,
            Action: policy.Action,
            AffectedItems: affectedItems,
            TotalSizeBytes: affectedItems.Sum(i => i.SizeBytes),
            PreviewedAt: DateTimeOffset.UtcNow);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger?.LogInformation("Retention policy enforcer is disabled");
            return;
        }

        _logger?.LogInformation("Retention policy enforcer started with interval {Interval}", _options.EnforcementInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnforcePoliciesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during retention policy enforcement");
            }

            await Task.Delay(_options.EnforcementInterval, stoppingToken);
        }

        _logger?.LogInformation("Retention policy enforcer stopped");
    }

    private void InitializePolicies()
    {
        foreach (var config in _options.Policies.Where(p => p.IsEnabled))
        {
            var policyId = Guid.NewGuid().ToString();

            var policy = new RetentionPolicy(
                PolicyId: policyId,
                Name: config.Name,
                Description: config.Description,
                RetentionPeriod: config.RetentionPeriod,
                Action: config.Action,
                TargetPaths: config.TargetPaths,
                FilePatterns: config.FilePatterns,
                IsEnabled: true,
                CreatedAt: DateTimeOffset.UtcNow,
                LastEnforcedAt: null);

            _policies[policyId] = policy;

            _logger?.LogInformation("Initialized retention policy {PolicyName} with period {Period}",
                config.Name, config.RetentionPeriod);
        }
    }

    private async Task<PolicyEnforcementResult> EnforcePolicyAsync(
        RetentionPolicy policy,
        CancellationToken cancellationToken)
    {
        var itemsEvaluated = 0;
        var itemsDeleted = 0;
        var itemsArchived = 0;
        var itemsSkipped = 0;
        var errors = new List<string>();

        var affectedItems = await GetAffectedItemsAsync(policy, cancellationToken);

        foreach (var item in affectedItems)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            itemsEvaluated++;

            // Check if item is on hold
            if (await IsPathOnHoldAsync(item.Path, cancellationToken))
            {
                itemsSkipped++;
                continue;
            }

            try
            {
                switch (policy.Action)
                {
                    case RetentionAction.Delete:
                        await DeleteItemAsync(item, cancellationToken);
                        itemsDeleted++;
                        break;

                    case RetentionAction.SoftDelete:
                        await SoftDeleteItemAsync(item, cancellationToken);
                        itemsDeleted++;
                        break;

                    case RetentionAction.Archive:
                        await ArchiveItemAsync(item, cancellationToken);
                        itemsArchived++;
                        break;

                    case RetentionAction.Notify:
                        // Just log, actual notification would be handled by notification service
                        _logger?.LogInformation("Item {Path} past retention: {Policy}", item.Path, policy.Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Item {item.Path}: {ex.Message}");
                _logger?.LogError(ex, "Error processing item {Path} for policy {Policy}", item.Path, policy.Name);
            }
        }

        return new PolicyEnforcementResult(
            ItemsEvaluated: itemsEvaluated,
            ItemsDeleted: itemsDeleted,
            ItemsArchived: itemsArchived,
            ItemsSkipped: itemsSkipped,
            Errors: errors);
    }

    private Task<List<RetentionItem>> GetAffectedItemsAsync(
        RetentionPolicy policy,
        CancellationToken cancellationToken)
    {
        // In production, this would query the actual storage/database
        // For now, return simulated items
        var items = new List<RetentionItem>
        {
            new RetentionItem(
                Path: "/archive/2023/old-data.zip",
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-400),
                LastAccessedAt: DateTimeOffset.UtcNow.AddDays(-300),
                SizeBytes: 1024 * 1024 * 100)
        };

        var cutoffDate = DateTimeOffset.UtcNow.Subtract(policy.RetentionPeriod);
        var affectedItems = items.Where(i => i.CreatedAt < cutoffDate).ToList();

        return Task.FromResult(affectedItems);
    }

    private Task DeleteItemAsync(RetentionItem item, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Deleting item {Path} (Size: {Size} bytes)", item.Path, item.SizeBytes);
        return Task.CompletedTask;
    }

    private Task SoftDeleteItemAsync(RetentionItem item, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Soft deleting item {Path} with grace period {GracePeriod}",
            item.Path, _options.SoftDeleteGracePeriod);
        return Task.CompletedTask;
    }

    private Task ArchiveItemAsync(RetentionItem item, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Archiving item {Path} to cold storage", item.Path);
        return Task.CompletedTask;
    }
}

public interface IRetentionPolicyEnforcer
{
    Task<string> CreatePolicyAsync(string name, string description, TimeSpan retentionPeriod, RetentionAction action, IEnumerable<string>? targetPaths = null, IEnumerable<string>? filePatterns = null, CancellationToken cancellationToken = default);
    Task<bool> UpdatePolicyAsync(string policyId, string? name = null, string? description = null, TimeSpan? retentionPeriod = null, RetentionAction? action = null, CancellationToken cancellationToken = default);
    Task<bool> DeletePolicyAsync(string policyId, CancellationToken cancellationToken = default);
    Task<bool> EnablePolicyAsync(string policyId, CancellationToken cancellationToken = default);
    Task<bool> DisablePolicyAsync(string policyId, CancellationToken cancellationToken = default);
    Task<RetentionPolicy?> GetPolicyAsync(string policyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetentionPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken = default);
    Task<string> CreateHoldAsync(string name, string reason, IEnumerable<string> targetPaths, DateTimeOffset? expiresAt = null, CancellationToken cancellationToken = default);
    Task<bool> ReleaseHoldAsync(string holdId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetentionHold>> GetActiveHoldsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsPathOnHoldAsync(string path, CancellationToken cancellationToken = default);
    Task<RetentionEnforcementResult> EnforcePoliciesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetentionEnforcementResult>> GetEnforcementHistoryAsync(int count = 50, CancellationToken cancellationToken = default);
    Task<RetentionPreviewResult> PreviewEnforcementAsync(string policyId, CancellationToken cancellationToken = default);
}

public record RetentionPolicy(
    string PolicyId,
    string Name,
    string Description,
    TimeSpan RetentionPeriod,
    RetentionAction Action,
    List<string> TargetPaths,
    List<string> FilePatterns,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastEnforcedAt);

public record RetentionHold(
    string HoldId,
    string Name,
    string Reason,
    List<string> TargetPaths,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsActive);

public record RetentionEnforcementResult(
    string EnforcementId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int PoliciesEnforced,
    int ItemsEvaluated,
    int ItemsDeleted,
    int ItemsArchived,
    int ItemsSkipped,
    IReadOnlyList<string> Errors);

public record PolicyEnforcementResult(
    int ItemsEvaluated,
    int ItemsDeleted,
    int ItemsArchived,
    int ItemsSkipped,
    IReadOnlyList<string> Errors);

public record RetentionPreviewResult(
    string PolicyId,
    string PolicyName,
    RetentionAction Action,
    IReadOnlyList<RetentionItem> AffectedItems,
    long TotalSizeBytes,
    DateTimeOffset PreviewedAt);

public record RetentionItem(
    string Path,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastAccessedAt,
    long SizeBytes);

public enum RetentionAction
{
    Delete,
    SoftDelete,
    Archive,
    Notify
}
