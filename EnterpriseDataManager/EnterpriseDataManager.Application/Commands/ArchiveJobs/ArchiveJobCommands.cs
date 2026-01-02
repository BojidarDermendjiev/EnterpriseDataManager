namespace EnterpriseDataManager.Application.Commands.ArchiveJobs;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Enums;

public sealed record CreateArchiveJobCommand(
    Guid ArchivePlanId,
    JobPriority Priority = JobPriority.Normal,
    DateTimeOffset? ScheduledAt = null,
    string? TargetPath = null) : ICommand<ArchiveJobDto>;

public sealed record StartArchiveJobCommand(Guid Id) : ICommand<ArchiveJobDto>;

public sealed record CompleteArchiveJobCommand(Guid Id) : ICommand<ArchiveJobDto>;

public sealed record FailArchiveJobCommand(
    Guid Id,
    string Reason) : ICommand<ArchiveJobDto>;

public sealed record CancelArchiveJobCommand(Guid Id) : ICommand<ArchiveJobDto>;

public sealed record ScheduleArchiveJobCommand(
    Guid Id,
    DateTimeOffset? ScheduledTime = null) : ICommand<ArchiveJobDto>;

public sealed record AddArchiveItemCommand(
    Guid JobId,
    string SourcePath,
    string TargetPath,
    long SizeBytes) : ICommand<ArchiveItemDto>;

public sealed record RecordArchiveItemSuccessCommand(
    Guid JobId,
    Guid ItemId,
    string? Hash = null) : ICommand;

public sealed record RecordArchiveItemFailureCommand(
    Guid JobId,
    Guid ItemId,
    string Error) : ICommand;

public sealed record ProcessScheduledJobsCommand : ICommand;
