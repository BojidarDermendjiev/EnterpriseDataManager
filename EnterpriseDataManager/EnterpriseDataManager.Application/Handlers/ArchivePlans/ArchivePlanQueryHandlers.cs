namespace EnterpriseDataManager.Application.Handlers.ArchivePlans;

using AutoMapper;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;
using EnterpriseDataManager.Application.Queries.ArchivePlans;
using EnterpriseDataManager.Core.Interfaces.Repositories;

public sealed class GetArchivePlanByIdQueryHandler : IQueryHandler<GetArchivePlanByIdQuery, ArchivePlanDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchivePlanByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchivePlanDto?> Handle(GetArchivePlanByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(request.Id, cancellationToken);
        return plan is null ? null : _mapper.Map<ArchivePlanDto>(plan);
    }
}

public sealed class GetAllArchivePlansQueryHandler : IQueryHandler<GetAllArchivePlansQuery, IReadOnlyList<ArchivePlanDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetAllArchivePlansQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ArchivePlanDto>> Handle(GetAllArchivePlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await _unitOfWork.ArchivePlans.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<ArchivePlanDto>>(plans);
    }
}

public sealed class GetActiveArchivePlansQueryHandler : IQueryHandler<GetActiveArchivePlansQuery, IReadOnlyList<ArchivePlanDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetActiveArchivePlansQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ArchivePlanDto>> Handle(GetActiveArchivePlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await _unitOfWork.ArchivePlans.GetActivePlansAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<ArchivePlanDto>>(plans);
    }
}

public sealed class GetArchivePlanSummariesQueryHandler : IQueryHandler<GetArchivePlanSummariesQuery, IReadOnlyList<ArchivePlanSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchivePlanSummariesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ArchivePlanSummaryDto>> Handle(GetArchivePlanSummariesQuery request, CancellationToken cancellationToken)
    {
        var plans = await _unitOfWork.ArchivePlans.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<ArchivePlanSummaryDto>>(plans);
    }
}

public sealed class GetArchivePlansPagedQueryHandler : IQueryHandler<GetArchivePlansPagedQuery, PagedResultDto<ArchivePlanSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchivePlansPagedQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<ArchivePlanSummaryDto>> Handle(GetArchivePlansPagedQuery request, CancellationToken cancellationToken)
    {
        var allPlans = await _unitOfWork.ArchivePlans.GetAllAsync(cancellationToken);

        var query = allPlans.AsQueryable();

        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLowerInvariant().Contains(term) ||
                (p.Description != null && p.Description.ToLowerInvariant().Contains(term)));
        }

        var totalCount = query.Count();
        var items = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = _mapper.Map<List<ArchivePlanSummaryDto>>(items);

        return PagedResultDto<ArchivePlanSummaryDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

public sealed class GetArchivePlansByStorageProviderQueryHandler : IQueryHandler<GetArchivePlansByStorageProviderQuery, IReadOnlyList<ArchivePlanSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchivePlansByStorageProviderQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ArchivePlanSummaryDto>> Handle(GetArchivePlansByStorageProviderQuery request, CancellationToken cancellationToken)
    {
        var plans = await _unitOfWork.ArchivePlans.GetByStorageProviderAsync(request.StorageProviderId, cancellationToken);
        return _mapper.Map<IReadOnlyList<ArchivePlanSummaryDto>>(plans);
    }
}

public sealed class GetArchivePlansByRetentionPolicyQueryHandler : IQueryHandler<GetArchivePlansByRetentionPolicyQuery, IReadOnlyList<ArchivePlanSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchivePlansByRetentionPolicyQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ArchivePlanSummaryDto>> Handle(GetArchivePlansByRetentionPolicyQuery request, CancellationToken cancellationToken)
    {
        var plans = await _unitOfWork.ArchivePlans.GetByRetentionPolicyAsync(request.RetentionPolicyId, cancellationToken);
        return _mapper.Map<IReadOnlyList<ArchivePlanSummaryDto>>(plans);
    }
}
