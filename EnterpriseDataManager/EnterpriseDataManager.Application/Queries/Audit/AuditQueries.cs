namespace EnterpriseDataManager.Application.Queries.Audit;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;

public sealed record GetAuditRecordByIdQuery(Guid Id) : IQuery<AuditRecordDto?>;

public sealed record SearchAuditRecordsQuery(
    string? Actor = null,
    string? Action = null,
    string? ResourceType = null,
    string? ResourceId = null,
    bool? Success = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? IpAddress = null,
    string? CorrelationId = null,
    int Skip = 0,
    int Take = 50) : IQuery<IReadOnlyList<AuditRecordDto>>;

public sealed record GetAuditRecordsPagedQuery(
    int PageNumber = 1,
    int PageSize = 50,
    string? Actor = null,
    string? Action = null,
    string? ResourceType = null,
    bool? Success = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<PagedResultDto<AuditRecordSummaryDto>>;

public sealed record GetAuditTrailQuery(
    string ResourceType,
    string ResourceId) : IQuery<IReadOnlyList<AuditRecordDto>>;

public sealed record GetUserActivityQuery(
    string Actor,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<IReadOnlyList<AuditRecordDto>>;

public sealed record GetAuditSummaryQuery(
    DateTimeOffset From,
    DateTimeOffset To) : IQuery<AuditSummaryDto>;
