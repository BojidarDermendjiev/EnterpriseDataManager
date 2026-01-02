namespace EnterpriseDataManager.Application.Commands.ArchivePlans;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Enums;

public sealed record CreateArchivePlanCommand(
    string Name,
    string SourcePath,
    string? Description = null,
    SecurityLevel SecurityLevel = SecurityLevel.Internal,
    Guid? StorageProviderId = null,
    Guid? RetentionPolicyId = null,
    string? Schedule = null,
    string? ScheduleDescription = null) : ICommand<ArchivePlanDto>;

public sealed record UpdateArchivePlanCommand(
    Guid Id,
    string Name,
    string? Description = null,
    SecurityLevel? SecurityLevel = null,
    Guid? StorageProviderId = null,
    Guid? RetentionPolicyId = null,
    string? Schedule = null,
    string? ScheduleDescription = null) : ICommand<ArchivePlanDto>;

public sealed record DeleteArchivePlanCommand(Guid Id) : ICommand;

public sealed record ActivateArchivePlanCommand(Guid Id) : ICommand<ArchivePlanDto>;

public sealed record DeactivateArchivePlanCommand(Guid Id) : ICommand<ArchivePlanDto>;

public sealed record SetArchivePlanScheduleCommand(
    Guid Id,
    string CronExpression,
    string? Description = null) : ICommand<ArchivePlanDto>;

public sealed record ClearArchivePlanScheduleCommand(Guid Id) : ICommand<ArchivePlanDto>;
