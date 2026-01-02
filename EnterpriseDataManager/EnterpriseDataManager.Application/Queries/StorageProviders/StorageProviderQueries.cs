namespace EnterpriseDataManager.Application.Queries.StorageProviders;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;
using EnterpriseDataManager.Core.Enums;

public sealed record GetStorageProviderByIdQuery(Guid Id) : IQuery<StorageProviderDto?>;

public sealed record GetAllStorageProvidersQuery : IQuery<IReadOnlyList<StorageProviderDto>>;

public sealed record GetEnabledStorageProvidersQuery : IQuery<IReadOnlyList<StorageProviderDto>>;

public sealed record GetStorageProviderSummariesQuery : IQuery<IReadOnlyList<StorageProviderSummaryDto>>;

public sealed record GetStorageProvidersPagedQuery(
    int PageNumber = 1,
    int PageSize = 20,
    StorageType? Type = null,
    bool? IsEnabled = null) : IQuery<PagedResultDto<StorageProviderSummaryDto>>;

public sealed record GetStorageHealthQuery(Guid ProviderId) : IQuery<StorageHealthDto>;

public sealed record GetStorageUsageQuery(Guid ProviderId) : IQuery<StorageUsageDto>;

public sealed record GetAllStorageHealthQuery : IQuery<IReadOnlyList<StorageHealthDto>>;
