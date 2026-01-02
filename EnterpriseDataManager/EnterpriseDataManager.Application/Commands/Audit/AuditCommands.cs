namespace EnterpriseDataManager.Application.Commands.Audit;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;

public sealed record CreateAuditRecordCommand(
    string Actor,
    string Action,
    bool Success = true,
    string? ResourceType = null,
    string? ResourceId = null,
    string? Details = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? CorrelationId = null) : ICommand<AuditRecordDto>;

public sealed record PurgeOldAuditRecordsCommand(int RetentionDays) : ICommand<int>;
