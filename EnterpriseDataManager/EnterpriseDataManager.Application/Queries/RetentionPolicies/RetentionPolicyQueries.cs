namespace EnterpriseDataManager.Application.Queries.RetentionPolicies;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;

public sealed record GetRetentionPolicyByIdQuery(Guid Id) : IQuery<RetentionPolicyDto?>;

public sealed record GetAllRetentionPoliciesQuery : IQuery<IReadOnlyList<RetentionPolicyDto>>;

public sealed record GetRetentionPolicySummariesQuery : IQuery<IReadOnlyList<RetentionPolicySummaryDto>>;

public sealed record GetRetentionPoliciesPagedQuery(
    int PageNumber = 1,
    int PageSize = 20,
    bool? IsLegalHold = null,
    bool? IsImmutable = null) : IQuery<PagedResultDto<RetentionPolicySummaryDto>>;

public sealed record GetLegalHoldPoliciesQuery : IQuery<IReadOnlyList<RetentionPolicyDto>>;

public sealed record GetExpiredArchivesQuery : IQuery<IReadOnlyList<ArchiveJobDto>>;

public sealed record GetArchivesApproachingExpiryQuery(
    int WarningDays = 30) : IQuery<IReadOnlyList<ArchiveJobDto>>;
