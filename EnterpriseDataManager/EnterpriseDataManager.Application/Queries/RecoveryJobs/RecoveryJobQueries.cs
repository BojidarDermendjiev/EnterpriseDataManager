namespace EnterpriseDataManager.Application.Queries.RecoveryJobs;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;
using EnterpriseDataManager.Core.Enums;

public sealed record GetRecoveryJobByIdQuery(Guid Id) : IQuery<RecoveryJobDto?>;

public sealed record GetRecoveryJobsQuery(
    Guid? ArchiveJobId = null,
    ArchiveStatus? Status = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null) : IQuery<IReadOnlyList<RecoveryJobDto>>;

public sealed record GetRunningRecoveryJobsQuery : IQuery<IReadOnlyList<RecoveryJobDto>>;

public sealed record GetRecoveryJobsPagedQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? ArchiveJobId = null,
    ArchiveStatus? Status = null) : IQuery<PagedResultDto<RecoveryJobSummaryDto>>;
