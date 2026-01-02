namespace EnterpriseDataManager.Application.Queries.ArchiveJobs;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;
using EnterpriseDataManager.Core.Enums;

public sealed record GetArchiveJobByIdQuery(Guid Id) : IQuery<ArchiveJobDto?>;

public sealed record GetArchiveJobsQuery(
    Guid? ArchivePlanId = null,
    ArchiveStatus? Status = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null) : IQuery<IReadOnlyList<ArchiveJobDto>>;

public sealed record GetRunningArchiveJobsQuery : IQuery<IReadOnlyList<ArchiveJobDto>>;

public sealed record GetArchiveJobsPagedQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? ArchivePlanId = null,
    ArchiveStatus? Status = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null) : IQuery<PagedResultDto<ArchiveJobSummaryDto>>;

public sealed record GetArchiveJobItemsQuery(Guid JobId) : IQuery<IReadOnlyList<ArchiveItemDto>>;

public sealed record GetArchiveJobProgressQuery(Guid JobId) : IQuery<JobProgressDto?>;

public sealed record GetArchiveJobItemsPagedQuery(
    Guid JobId,
    int PageNumber = 1,
    int PageSize = 50,
    bool? Success = null) : IQuery<PagedResultDto<ArchiveItemDto>>;
