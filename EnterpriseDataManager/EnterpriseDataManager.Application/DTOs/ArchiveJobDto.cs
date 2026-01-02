namespace EnterpriseDataManager.Application.DTOs;

using EnterpriseDataManager.Core.Enums;

public sealed record ArchiveJobDto
{
    public Guid Id { get; init; }
    public Guid ArchivePlanId { get; init; }
    public string? ArchivePlanName { get; init; }
    public ArchiveStatus Status { get; init; }
    public JobPriority Priority { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? FailureReason { get; init; }
    public int TotalItemCount { get; init; }
    public int ProcessedItemCount { get; init; }
    public int FailedItemCount { get; init; }
    public long TotalBytes { get; init; }
    public long ProcessedBytes { get; init; }
    public string? TargetPath { get; init; }
    public double ProgressPercentage { get; init; }
    public TimeSpan? Duration { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
}

public sealed record ArchiveJobSummaryDto
{
    public Guid Id { get; init; }
    public Guid ArchivePlanId { get; init; }
    public string? ArchivePlanName { get; init; }
    public ArchiveStatus Status { get; init; }
    public JobPriority Priority { get; init; }
    public double ProgressPercentage { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
}

public sealed record CreateArchiveJobDto
{
    public Guid ArchivePlanId { get; init; }
    public JobPriority Priority { get; init; } = JobPriority.Normal;
    public DateTimeOffset? ScheduledAt { get; init; }
    public string? TargetPath { get; init; }
}

public sealed record ArchiveItemDto
{
    public Guid Id { get; init; }
    public Guid ArchiveJobId { get; init; }
    public string SourcePath { get; init; } = default!;
    public string TargetPath { get; init; } = default!;
    public long SizeBytes { get; init; }
    public string? Hash { get; init; }
    public bool? Success { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
}

public sealed record JobProgressDto
{
    public Guid JobId { get; init; }
    public ArchiveStatus Status { get; init; }
    public int TotalItems { get; init; }
    public int ProcessedItems { get; init; }
    public int FailedItems { get; init; }
    public long TotalBytes { get; init; }
    public long ProcessedBytes { get; init; }
    public double ProgressPercentage { get; init; }
    public TimeSpan? ElapsedTime { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
}
