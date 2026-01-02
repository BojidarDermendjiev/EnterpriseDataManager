namespace EnterpriseDataManager.Application.DTOs;

using EnterpriseDataManager.Core.Enums;

public sealed record ArchivePlanDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string? Schedule { get; init; }
    public string? ScheduleDescription { get; init; }
    public string SourcePath { get; init; } = default!;
    public bool IsActive { get; init; }
    public SecurityLevel SecurityLevel { get; init; }
    public Guid? StorageProviderId { get; init; }
    public string? StorageProviderName { get; init; }
    public Guid? RetentionPolicyId { get; init; }
    public string? RetentionPolicyName { get; init; }
    public DateTimeOffset? LastRunAt { get; init; }
    public DateTimeOffset? NextRunAt { get; init; }
    public int JobCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

public sealed record ArchivePlanSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public bool IsActive { get; init; }
    public SecurityLevel SecurityLevel { get; init; }
    public DateTimeOffset? LastRunAt { get; init; }
    public DateTimeOffset? NextRunAt { get; init; }
}

public sealed record CreateArchivePlanDto
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string SourcePath { get; init; } = default!;
    public SecurityLevel SecurityLevel { get; init; } = SecurityLevel.Internal;
    public Guid? StorageProviderId { get; init; }
    public Guid? RetentionPolicyId { get; init; }
    public string? Schedule { get; init; }
    public string? ScheduleDescription { get; init; }
}

public sealed record UpdateArchivePlanDto
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public SecurityLevel? SecurityLevel { get; init; }
    public Guid? StorageProviderId { get; init; }
    public Guid? RetentionPolicyId { get; init; }
    public string? Schedule { get; init; }
    public string? ScheduleDescription { get; init; }
}
