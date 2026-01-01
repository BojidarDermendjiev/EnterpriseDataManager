namespace EnterpriseDataManager.Core.Interfaces.Services;

using EnterpriseDataManager.Core.Entities;

public interface IAuditService
{
    Task<AuditRecord> LogAsync(string actor, string action, bool success = true, CancellationToken cancellationToken = default);
    Task<AuditRecord> LogWithResourceAsync(string actor, string action, string resourceType, string resourceId, bool success = true, CancellationToken cancellationToken = default);
    Task<AuditRecord> LogWithDetailsAsync(string actor, string action, string details, bool success = true, CancellationToken cancellationToken = default);
    Task<AuditRecord> LogSecurityEventAsync(string actor, string action, string details, string? ipAddress = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> GetAuditTrailAsync(string resourceType, string resourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> GetUserActivityAsync(string actor, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> SearchAuditLogsAsync(AuditSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<AuditSummary> GetAuditSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task PurgeOldRecordsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}

public record AuditSearchCriteria(
    string? Actor = null,
    string? Action = null,
    string? ResourceType = null,
    string? ResourceId = null,
    bool? Success = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? IpAddress = null,
    string? CorrelationId = null,
    int? Skip = null,
    int? Take = null);

public record AuditSummary(
    int TotalRecords,
    int SuccessfulActions,
    int FailedActions,
    Dictionary<string, int> ActionCounts,
    Dictionary<string, int> ActorCounts,
    IReadOnlyList<AuditRecord> RecentFailures);
