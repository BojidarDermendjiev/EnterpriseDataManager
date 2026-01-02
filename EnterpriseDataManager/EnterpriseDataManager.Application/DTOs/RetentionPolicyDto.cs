namespace EnterpriseDataManager.Application.DTOs;

public sealed record RetentionPolicyDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public TimeSpan RetentionPeriod { get; init; }
    public int RetentionDays { get; init; }
    public bool IsLegalHold { get; init; }
    public bool IsImmutable { get; init; }
    public string? Scope { get; init; }
    public int ArchivePlanCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

public sealed record RetentionPolicySummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public int RetentionDays { get; init; }
    public bool IsLegalHold { get; init; }
    public bool IsImmutable { get; init; }
}

public sealed record CreateRetentionPolicyDto
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public int RetentionDays { get; init; }
    public string? Scope { get; init; }
}

public sealed record UpdateRetentionPolicyDto
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public int? RetentionDays { get; init; }
    public string? Scope { get; init; }
}
