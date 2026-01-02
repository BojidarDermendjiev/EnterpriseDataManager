namespace EnterpriseDataManager.Application.Queries.ArchivePlans;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;

public sealed record GetArchivePlanByIdQuery(Guid Id) : IQuery<ArchivePlanDto?>;

public sealed record GetAllArchivePlansQuery : IQuery<IReadOnlyList<ArchivePlanDto>>;

public sealed record GetActiveArchivePlansQuery : IQuery<IReadOnlyList<ArchivePlanDto>>;

public sealed record GetArchivePlanSummariesQuery : IQuery<IReadOnlyList<ArchivePlanSummaryDto>>;

public sealed record GetArchivePlansPagedQuery(
    int PageNumber = 1,
    int PageSize = 20,
    bool? IsActive = null,
    string? SearchTerm = null) : IQuery<PagedResultDto<ArchivePlanSummaryDto>>;

public sealed record GetArchivePlansByStorageProviderQuery(
    Guid StorageProviderId) : IQuery<IReadOnlyList<ArchivePlanSummaryDto>>;

public sealed record GetArchivePlansByRetentionPolicyQuery(
    Guid RetentionPolicyId) : IQuery<IReadOnlyList<ArchivePlanSummaryDto>>;
