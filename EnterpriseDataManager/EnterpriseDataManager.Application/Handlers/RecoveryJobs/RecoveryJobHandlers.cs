namespace EnterpriseDataManager.Application.Handlers.RecoveryJobs;

using AutoMapper;
using EnterpriseDataManager.Application.Commands.RecoveryJobs;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;
using EnterpriseDataManager.Application.Queries.RecoveryJobs;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;

public sealed class CreateRecoveryJobCommandHandler : ICommandHandler<CreateRecoveryJobCommand, RecoveryJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateRecoveryJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RecoveryJobDto> Handle(CreateRecoveryJobCommand request, CancellationToken cancellationToken)
    {
        var archiveJob = await _unitOfWork.ArchiveJobs.GetByIdAsync(request.ArchiveJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(request.ArchiveJobId);

        var job = RecoveryJob.Create(request.ArchiveJobId, request.DestinationPath);

        await _unitOfWork.RecoveryJobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RecoveryJobDto>(job);
    }
}

public sealed class StartRecoveryJobCommandHandler : ICommandHandler<StartRecoveryJobCommand, RecoveryJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public StartRecoveryJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RecoveryJobDto> Handle(StartRecoveryJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForRecoveryJob(request.Id);

        var archiveJob = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(job.ArchiveJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(job.ArchiveJobId);

        job.Start(archiveJob.Items.Count, archiveJob.ProcessedBytes);
        _unitOfWork.RecoveryJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RecoveryJobDto>(job);
    }
}

public sealed class CompleteRecoveryJobCommandHandler : ICommandHandler<CompleteRecoveryJobCommand, RecoveryJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CompleteRecoveryJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RecoveryJobDto> Handle(CompleteRecoveryJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForRecoveryJob(request.Id);

        job.Complete();
        _unitOfWork.RecoveryJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RecoveryJobDto>(job);
    }
}

public sealed class FailRecoveryJobCommandHandler : ICommandHandler<FailRecoveryJobCommand, RecoveryJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public FailRecoveryJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RecoveryJobDto> Handle(FailRecoveryJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForRecoveryJob(request.Id);

        job.Fail(request.Reason);
        _unitOfWork.RecoveryJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RecoveryJobDto>(job);
    }
}

public sealed class CancelRecoveryJobCommandHandler : ICommandHandler<CancelRecoveryJobCommand, RecoveryJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CancelRecoveryJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RecoveryJobDto> Handle(CancelRecoveryJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForRecoveryJob(request.Id);

        job.Cancel();
        _unitOfWork.RecoveryJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RecoveryJobDto>(job);
    }
}

public sealed class ValidateArchiveIntegrityCommandHandler : ICommandHandler<ValidateArchiveIntegrityCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public ValidateArchiveIntegrityCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ValidateArchiveIntegrityCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(request.ArchiveJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(request.ArchiveJobId);

        return job.Items.All(i => i.Success == true && !string.IsNullOrEmpty(i.Hash));
    }
}

public sealed class GetRecoveryJobByIdQueryHandler : IQueryHandler<GetRecoveryJobByIdQuery, RecoveryJobDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetRecoveryJobByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RecoveryJobDto?> Handle(GetRecoveryJobByIdQuery request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(request.Id, cancellationToken);
        return job is null ? null : _mapper.Map<RecoveryJobDto>(job);
    }
}

public sealed class GetRecoveryJobsQueryHandler : IQueryHandler<GetRecoveryJobsQuery, IReadOnlyList<RecoveryJobDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetRecoveryJobsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<RecoveryJobDto>> Handle(GetRecoveryJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await _unitOfWork.RecoveryJobs.GetAllAsync(cancellationToken);

        var query = jobs.AsQueryable();

        if (request.ArchiveJobId.HasValue)
        {
            query = query.Where(j => j.ArchiveJobId == request.ArchiveJobId.Value);
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

        return _mapper.Map<IReadOnlyList<RecoveryJobDto>>(query.ToList());
    }
}

public sealed class GetRunningRecoveryJobsQueryHandler : IQueryHandler<GetRunningRecoveryJobsQuery, IReadOnlyList<RecoveryJobDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetRunningRecoveryJobsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<RecoveryJobDto>> Handle(GetRunningRecoveryJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await _unitOfWork.RecoveryJobs.GetByStatusAsync(ArchiveStatus.Running, cancellationToken);
        return _mapper.Map<IReadOnlyList<RecoveryJobDto>>(jobs);
    }
}

public sealed class GetRecoveryJobsPagedQueryHandler : IQueryHandler<GetRecoveryJobsPagedQuery, PagedResultDto<RecoveryJobSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetRecoveryJobsPagedQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<RecoveryJobSummaryDto>> Handle(GetRecoveryJobsPagedQuery request, CancellationToken cancellationToken)
    {
        var allJobs = await _unitOfWork.RecoveryJobs.GetAllAsync(cancellationToken);

        var query = allJobs.AsQueryable();

        if (request.ArchiveJobId.HasValue)
        {
            query = query.Where(j => j.ArchiveJobId == request.ArchiveJobId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(j => j.Status == request.Status.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = _mapper.Map<List<RecoveryJobSummaryDto>>(items);

        return PagedResultDto<RecoveryJobSummaryDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
