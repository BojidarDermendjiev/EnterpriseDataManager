namespace EnterpriseDataManager.Infrastructure.Services;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

public sealed class AuditService : IAuditService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuditService>? _logger;

    public AuditService(
        IUnitOfWork unitOfWork,
        ILogger<AuditService>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AuditRecord> LogAsync(string actor, string action, bool success = true, CancellationToken cancellationToken = default)
    {
        var record = AuditRecord.Create(actor, action, success);
        await _unitOfWork.AuditRecords.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return record;
    }

    public async Task<AuditRecord> LogWithResourceAsync(string actor, string action, string resourceType, string resourceId, bool success = true, CancellationToken cancellationToken = default)
    {
        var record = AuditRecord.Create(actor, action, success)
            .WithResource(resourceType, resourceId);

        await _unitOfWork.AuditRecords.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return record;
    }

    public async Task<AuditRecord> LogWithDetailsAsync(string actor, string action, string details, bool success = true, CancellationToken cancellationToken = default)
    {
        var record = AuditRecord.Create(actor, action, success)
            .WithDetails(details);

        await _unitOfWork.AuditRecords.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return record;
    }

    public async Task<AuditRecord> LogSecurityEventAsync(string actor, string action, string details, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        var record = AuditRecord.Create(actor, action, true)
            .WithDetails(details)
            .WithClientInfo(ipAddress, null);

        await _unitOfWork.AuditRecords.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Security event logged: {Action} by {Actor} from {IpAddress}", action, actor, ipAddress ?? "unknown");
        return record;
    }

    public async Task<IReadOnlyList<AuditRecord>> GetAuditTrailAsync(string resourceType, string resourceId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.AuditRecords.GetByResourceAsync(resourceType, resourceId, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> GetUserActivityAsync(string actor, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        var records = await _unitOfWork.AuditRecords.GetByActorAsync(actor, cancellationToken);

        if (from.HasValue || to.HasValue)
        {
            records = records
                .Where(r => (!from.HasValue || r.Timestamp >= from.Value) && (!to.HasValue || r.Timestamp <= to.Value))
                .ToList();
        }

        return records;
    }

    public async Task<IReadOnlyList<AuditRecord>> SearchAuditLogsAsync(AuditSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AuditRecord> records;

        if (!string.IsNullOrEmpty(criteria.Actor))
        {
            records = await _unitOfWork.AuditRecords.GetByActorAsync(criteria.Actor, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(criteria.Action))
        {
            records = await _unitOfWork.AuditRecords.GetByActionAsync(criteria.Action, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(criteria.ResourceType) && !string.IsNullOrEmpty(criteria.ResourceId))
        {
            records = await _unitOfWork.AuditRecords.GetByResourceAsync(criteria.ResourceType, criteria.ResourceId, cancellationToken);
        }
        else if (criteria.From.HasValue && criteria.To.HasValue)
        {
            records = await _unitOfWork.AuditRecords.GetInDateRangeAsync(criteria.From.Value, criteria.To.Value, cancellationToken);
        }
        else
        {
            records = await _unitOfWork.AuditRecords.GetAllAsync(cancellationToken);
        }

        // Apply additional filters
        var filtered = records.AsEnumerable();

        if (criteria.Success.HasValue)
        {
            filtered = filtered.Where(r => r.Success == criteria.Success.Value);
        }

        if (criteria.Skip.HasValue)
        {
            filtered = filtered.Skip(criteria.Skip.Value);
        }

        if (criteria.Take.HasValue)
        {
            filtered = filtered.Take(criteria.Take.Value);
        }

        return filtered.ToList();
    }

    public async Task<AuditSummary> GetAuditSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var records = await _unitOfWork.AuditRecords.GetInDateRangeAsync(from, to, cancellationToken);

        var actionCounts = records
            .GroupBy(r => r.Action)
            .ToDictionary(g => g.Key, g => g.Count());

        var actorCounts = records
            .GroupBy(r => r.Actor)
            .ToDictionary(g => g.Key, g => g.Count());

        var failures = records
            .Where(r => !r.Success)
            .OrderByDescending(r => r.Timestamp)
            .Take(10)
            .ToList();

        return new AuditSummary(
            TotalRecords: records.Count,
            SuccessfulActions: records.Count(r => r.Success),
            FailedActions: records.Count(r => !r.Success),
            ActionCounts: actionCounts,
            ActorCounts: actorCounts,
            RecentFailures: failures);
    }

    public async Task PurgeOldRecordsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTimeOffset.UtcNow.Subtract(retentionPeriod);
        var oldRecords = await _unitOfWork.AuditRecords.GetInDateRangeAsync(DateTimeOffset.MinValue, cutoffDate, cancellationToken);

        _unitOfWork.AuditRecords.RemoveRange(oldRecords);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Purged {Count} audit records older than {CutoffDate}", oldRecords.Count, cutoffDate);
    }
}
