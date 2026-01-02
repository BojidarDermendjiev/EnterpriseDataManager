namespace EnterpriseDataManager.Application.Handlers.Audit;

using AutoMapper;
using EnterpriseDataManager.Application.Commands.Audit;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;
using EnterpriseDataManager.Application.Queries.Audit;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;

public sealed class CreateAuditRecordCommandHandler : ICommandHandler<CreateAuditRecordCommand, AuditRecordDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateAuditRecordCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<AuditRecordDto> Handle(CreateAuditRecordCommand request, CancellationToken cancellationToken)
    {
        var record = AuditRecord.Create(request.Actor, request.Action, request.Success);

        if (!string.IsNullOrWhiteSpace(request.ResourceType) && !string.IsNullOrWhiteSpace(request.ResourceId))
        {
            record.WithResource(request.ResourceType, request.ResourceId);
        }

        if (!string.IsNullOrWhiteSpace(request.Details))
        {
            record.WithDetails(request.Details);
        }

        if (!string.IsNullOrWhiteSpace(request.IpAddress) || !string.IsNullOrWhiteSpace(request.UserAgent))
        {
            record.WithClientInfo(request.IpAddress, request.UserAgent);
        }

        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            record.WithCorrelationId(request.CorrelationId);
        }

        await _unitOfWork.AuditRecords.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AuditRecordDto>(record);
    }
}

public sealed class PurgeOldAuditRecordsCommandHandler : ICommandHandler<PurgeOldAuditRecordsCommand, int>
{
    private readonly IAuditService _auditService;

    public PurgeOldAuditRecordsCommandHandler(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task<int> Handle(PurgeOldAuditRecordsCommand request, CancellationToken cancellationToken)
    {
        await _auditService.PurgeOldRecordsAsync(TimeSpan.FromDays(request.RetentionDays), cancellationToken);
        return 0;
    }
}

public sealed class GetAuditRecordByIdQueryHandler : IQueryHandler<GetAuditRecordByIdQuery, AuditRecordDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetAuditRecordByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<AuditRecordDto?> Handle(GetAuditRecordByIdQuery request, CancellationToken cancellationToken)
    {
        var record = await _unitOfWork.AuditRecords.GetByIdAsync(request.Id, cancellationToken);
        return record is null ? null : _mapper.Map<AuditRecordDto>(record);
    }
}

public sealed class SearchAuditRecordsQueryHandler : IQueryHandler<SearchAuditRecordsQuery, IReadOnlyList<AuditRecordDto>>
{
    private readonly IAuditService _auditService;
    private readonly IMapper _mapper;

    public SearchAuditRecordsQueryHandler(IAuditService auditService, IMapper mapper)
    {
        _auditService = auditService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<AuditRecordDto>> Handle(SearchAuditRecordsQuery request, CancellationToken cancellationToken)
    {
        var criteria = new AuditSearchCriteria(
            request.Actor,
            request.Action,
            request.ResourceType,
            request.ResourceId,
            request.Success,
            request.From,
            request.To,
            request.IpAddress,
            request.CorrelationId,
            request.Skip,
            request.Take);

        var records = await _auditService.SearchAuditLogsAsync(criteria, cancellationToken);
        return _mapper.Map<IReadOnlyList<AuditRecordDto>>(records);
    }
}

public sealed class GetAuditRecordsPagedQueryHandler : IQueryHandler<GetAuditRecordsPagedQuery, PagedResultDto<AuditRecordSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetAuditRecordsPagedQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<AuditRecordSummaryDto>> Handle(GetAuditRecordsPagedQuery request, CancellationToken cancellationToken)
    {
        var allRecords = await _unitOfWork.AuditRecords.GetAllAsync(cancellationToken);

        var query = allRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Actor))
        {
            query = query.Where(r => r.Actor.Contains(request.Actor, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(r => r.Action.Contains(request.Action, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.ResourceType))
        {
            query = query.Where(r => r.ResourceType == request.ResourceType);
        }

        if (request.Success.HasValue)
        {
            query = query.Where(r => r.Success == request.Success.Value);
        }

        if (request.From.HasValue)
        {
            query = query.Where(r => r.Timestamp >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(r => r.Timestamp <= request.To.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(r => r.Timestamp)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = _mapper.Map<List<AuditRecordSummaryDto>>(items);

        return PagedResultDto<AuditRecordSummaryDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

public sealed class GetAuditTrailQueryHandler : IQueryHandler<GetAuditTrailQuery, IReadOnlyList<AuditRecordDto>>
{
    private readonly IAuditService _auditService;
    private readonly IMapper _mapper;

    public GetAuditTrailQueryHandler(IAuditService auditService, IMapper mapper)
    {
        _auditService = auditService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<AuditRecordDto>> Handle(GetAuditTrailQuery request, CancellationToken cancellationToken)
    {
        var records = await _auditService.GetAuditTrailAsync(request.ResourceType, request.ResourceId, cancellationToken);
        return _mapper.Map<IReadOnlyList<AuditRecordDto>>(records);
    }
}

public sealed class GetUserActivityQueryHandler : IQueryHandler<GetUserActivityQuery, IReadOnlyList<AuditRecordDto>>
{
    private readonly IAuditService _auditService;
    private readonly IMapper _mapper;

    public GetUserActivityQueryHandler(IAuditService auditService, IMapper mapper)
    {
        _auditService = auditService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<AuditRecordDto>> Handle(GetUserActivityQuery request, CancellationToken cancellationToken)
    {
        var records = await _auditService.GetUserActivityAsync(request.Actor, request.From, request.To, cancellationToken);
        return _mapper.Map<IReadOnlyList<AuditRecordDto>>(records);
    }
}

public sealed class GetAuditSummaryQueryHandler : IQueryHandler<GetAuditSummaryQuery, AuditSummaryDto>
{
    private readonly IAuditService _auditService;
    private readonly IMapper _mapper;

    public GetAuditSummaryQueryHandler(IAuditService auditService, IMapper mapper)
    {
        _auditService = auditService;
        _mapper = mapper;
    }

    public async Task<AuditSummaryDto> Handle(GetAuditSummaryQuery request, CancellationToken cancellationToken)
    {
        var summary = await _auditService.GetAuditSummaryAsync(request.From, request.To, cancellationToken);
        return _mapper.Map<AuditSummaryDto>(summary);
    }
}
