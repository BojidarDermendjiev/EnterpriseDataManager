namespace EnterpriseDataManager.Application.DTOs;

public sealed record AuditRecordDto
{
    public Guid Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Actor { get; init; } = default!;
    public string Action { get; init; } = default!;
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public bool Success { get; init; }
    public string? Details { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed record AuditRecordSummaryDto
{
    public Guid Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Actor { get; init; } = default!;
    public string Action { get; init; } = default!;
    public bool Success { get; init; }
}

public sealed record CreateAuditRecordDto
{
    public string Actor { get; init; } = default!;
    public string Action { get; init; } = default!;
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public bool Success { get; init; } = true;
    public string? Details { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed record AuditSearchDto
{
    public string? Actor { get; init; }
    public string? Action { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public bool? Success { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 50;
}

public sealed record AuditSummaryDto
{
    public int TotalRecords { get; init; }
    public int SuccessfulActions { get; init; }
    public int FailedActions { get; init; }
    public Dictionary<string, int> ActionCounts { get; init; } = new();
    public Dictionary<string, int> ActorCounts { get; init; } = new();
    public IReadOnlyList<AuditRecordSummaryDto> RecentFailures { get; init; } = [];
}
