namespace EnterpriseDataManager.Application.Commands.RecoveryJobs;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;

public sealed record CreateRecoveryJobCommand(
    Guid ArchiveJobId,
    string DestinationPath,
    IReadOnlyList<string>? SelectedItemPaths = null) : ICommand<RecoveryJobDto>;

public sealed record StartRecoveryJobCommand(Guid Id) : ICommand<RecoveryJobDto>;

public sealed record CompleteRecoveryJobCommand(Guid Id) : ICommand<RecoveryJobDto>;

public sealed record FailRecoveryJobCommand(
    Guid Id,
    string Reason) : ICommand<RecoveryJobDto>;

public sealed record CancelRecoveryJobCommand(Guid Id) : ICommand<RecoveryJobDto>;

public sealed record ValidateArchiveIntegrityCommand(Guid ArchiveJobId) : ICommand<bool>;
