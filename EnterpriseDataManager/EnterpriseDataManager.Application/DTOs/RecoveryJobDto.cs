namespace EnterpriseDataManager.Application.DTOs;

using EnterpriseDataManager.Core.Enums;

public sealed record RecoveryJobDto
{
    public Guid Id { get; init; }
    public Guid ArchiveJobId { get; init; }
    public string DestinationPath { get; init; } = default!;
    public ArchiveStatus Status { get; init; }
    public string? FailureReason { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int TotalItems { get; init; }
    public int RecoveredItems { get; init; }
    public long TotalBytes { get; init; }
    public long RecoveredBytes { get; init; }
    public double ProgressPercentage { get; init; }
    public TimeSpan? Duration { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
}

public sealed record RecoveryJobSummaryDto
{
    public Guid Id { get; init; }
    public Guid ArchiveJobId { get; init; }
    public ArchiveStatus Status { get; init; }
    public double ProgressPercentage { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
}

public sealed record CreateRecoveryJobDto
{
    public Guid ArchiveJobId { get; init; }
    public string DestinationPath { get; init; } = default!;
    public IReadOnlyList<string>? SelectedItemPaths { get; init; }
}
