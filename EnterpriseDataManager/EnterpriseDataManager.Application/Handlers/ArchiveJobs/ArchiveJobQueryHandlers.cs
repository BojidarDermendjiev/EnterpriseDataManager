namespace EnterpriseDataManager.Application.Handlers.ArchiveJobs;

using AutoMapper;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;
using EnterpriseDataManager.Application.Queries.ArchiveJobs;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Interfaces.Repositories;

public sealed class GetArchiveJobByIdQueryHandler : IQueryHandler<GetArchiveJobByIdQuery, ArchiveJobDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchiveJobByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchiveJobDto?> Handle(GetArchiveJobByIdQuery request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(request.Id, cancellationToken);
        return job is null ? null : _mapper.Map<ArchiveJobDto>(job);
    }
}

public sealed class GetArchiveJobsQueryHandler : IQueryHandler<GetArchiveJobsQuery, IReadOnlyList<ArchiveJobDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchiveJobsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ArchiveJobDto>> Handle(GetArchiveJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await _unitOfWork.ArchiveJobs.GetAllAsync(cancellationToken);

        var query = jobs.AsQueryable();

        if (request.ArchivePlanId.HasValue)
        {
            query = query.Where(j => j.ArchivePlanId == request.ArchivePlanId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(j => j.Status == request.Status.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(j => j.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(j => j.CreatedAt <= request.ToDate.Value);
        }

        return _mapper.Map<IReadOnlyList<ArchiveJobDto>>(query.ToList());
    }
}

public sealed class GetRunningArchiveJobsQueryHandler : IQueryHandler<GetRunningArchiveJobsQuery, IReadOnlyList<ArchiveJobDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetRunningArchiveJobsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ArchiveJobDto>> Handle(GetRunningArchiveJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await _unitOfWork.ArchiveJobs.GetByStatusAsync(ArchiveStatus.Running, cancellationToken);
        return _mapper.Map<IReadOnlyList<ArchiveJobDto>>(jobs);
    }
}

public sealed class GetArchiveJobsPagedQueryHandler : IQueryHandler<GetArchiveJobsPagedQuery, PagedResultDto<ArchiveJobSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchiveJobsPagedQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<ArchiveJobSummaryDto>> Handle(GetArchiveJobsPagedQuery request, CancellationToken cancellationToken)
    {
        var allJobs = await _unitOfWork.ArchiveJobs.GetAllAsync(cancellationToken);

        var query = allJobs.AsQueryable();

        if (request.ArchivePlanId.HasValue)
        {
            query = query.Where(j => j.ArchivePlanId == request.ArchivePlanId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(j => j.Status == request.Status.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(j => j.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(j => j.CreatedAt <= request.ToDate.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = _mapper.Map<List<ArchiveJobSummaryDto>>(items);

        return PagedResultDto<ArchiveJobSummaryDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

public sealed class GetArchiveJobItemsQueryHandler : IQueryHandler<GetArchiveJobItemsQuery, IReadOnlyList<ArchiveItemDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchiveJobItemsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ArchiveItemDto>> Handle(GetArchiveJobItemsQuery request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(request.JobId, cancellationToken);
        if (job is null)
        {
            return Array.Empty<ArchiveItemDto>();
        }

        return _mapper.Map<IReadOnlyList<ArchiveItemDto>>(job.Items);
    }
}

public sealed class GetArchiveJobProgressQueryHandler : IQueryHandler<GetArchiveJobProgressQuery, JobProgressDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchiveJobProgressQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<JobProgressDto?> Handle(GetArchiveJobProgressQuery request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(request.JobId, cancellationToken);
        return job is null ? null : _mapper.Map<JobProgressDto>(job);
    }
}

public sealed class GetArchiveJobItemsPagedQueryHandler : IQueryHandler<GetArchiveJobItemsPagedQuery, PagedResultDto<ArchiveItemDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetArchiveJobItemsPagedQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<ArchiveItemDto>> Handle(GetArchiveJobItemsPagedQuery request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(request.JobId, cancellationToken);
        if (job is null)
        {
            return PagedResultDto<ArchiveItemDto>.Create([], 0, request.PageNumber, request.PageSize);
        }

        var query = job.Items.AsQueryable();

        if (request.Success.HasValue)
        {
            query = query.Where(i => i.Success == request.Success.Value);
        }

        var totalCount = query.Count();
        var items = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = _mapper.Map<List<ArchiveItemDto>>(items);

        return PagedResultDto<ArchiveItemDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
