namespace EnterpriseDataManager.Application.Services;

using AutoMapper;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;

public interface IAuditAppService
{
    Task LogActionAsync(string action, bool success = true, string? details = null, CancellationToken cancellationToken = default);
    Task LogResourceAccessAsync(string action, string resourceType, string resourceId, bool success = true, CancellationToken cancellationToken = default);
    Task<AuditSummaryDto> GetDailySummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecordDto>> GetRecentFailuresAsync(int count = 10, CancellationToken cancellationToken = default);
}

public sealed class AuditAppService : IAuditAppService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMapper _mapper;

    public AuditAppService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _mapper = mapper;
    }

    public async Task LogActionAsync(
        string action,
        bool success = true,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var actor = _currentUserService.UserName ?? "System";

        var record = AuditRecord.Create(actor, action, success);

        if (!string.IsNullOrWhiteSpace(details))
        {
            record.WithDetails(details);
        }

        record.WithClientInfo(_currentUserService.IpAddress, _currentUserService.UserAgent);

        await _unitOfWork.AuditRecords.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task LogResourceAccessAsync(
        string action,
        string resourceType,
        string resourceId,
        bool success = true,
        CancellationToken cancellationToken = default)
    {
        var actor = _currentUserService.UserName ?? "System";

        await _auditService.LogWithResourceAsync(
            actor,
            action,
            resourceType,
            resourceId,
            success,
            cancellationToken);
    }

    public async Task<AuditSummaryDto> GetDailySummaryAsync(CancellationToken cancellationToken = default)
    {
        var today = _dateTimeProvider.Today;
        var from = new DateTimeOffset(today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        var summary = await _auditService.GetAuditSummaryAsync(from, to, cancellationToken);
        return _mapper.Map<AuditSummaryDto>(summary);
    }

    public async Task<IReadOnlyList<AuditRecordDto>> GetRecentFailuresAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        var criteria = new AuditSearchCriteria(
            Success: false,
            Take: count);

        var records = await _auditService.SearchAuditLogsAsync(criteria, cancellationToken);
        return _mapper.Map<IReadOnlyList<AuditRecordDto>>(records);
    }
}
