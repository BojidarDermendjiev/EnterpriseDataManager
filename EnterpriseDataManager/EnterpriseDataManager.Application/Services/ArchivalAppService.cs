namespace EnterpriseDataManager.Application.Services;

using AutoMapper;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.ValueObjects;

public interface IArchivalAppService
{
    Task<ArchiveJobDto> CreateAndStartJobAsync(Guid archivePlanId, JobPriority priority = JobPriority.Normal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveItemDto>> ArchiveFilesAsync(Guid jobId, IEnumerable<string> sourcePaths, CancellationToken cancellationToken = default);
    Task<ArchiveJobDto> FinalizeJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}

public sealed class ArchivalAppService : IArchivalAppService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IArchivalService _archivalService;
    private readonly IMapper _mapper;

    public ArchivalAppService(
        IUnitOfWork unitOfWork,
        IArchivalService archivalService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _archivalService = archivalService;
        _mapper = mapper;
    }

    public async Task<ArchiveJobDto> CreateAndStartJobAsync(
        Guid archivePlanId,
        JobPriority priority = JobPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        var job = await _archivalService.CreateJobAsync(archivePlanId, priority, cancellationToken);
        var startedJob = await _archivalService.StartJobAsync(job.Id, cancellationToken);
        return _mapper.Map<ArchiveJobDto>(startedJob);
    }

    public async Task<IReadOnlyList<ArchiveItemDto>> ArchiveFilesAsync(
        Guid jobId,
        IEnumerable<string> sourcePaths,
        CancellationToken cancellationToken = default)
    {
        var results = await _archivalService.ArchiveItemsAsync(jobId, sourcePaths, cancellationToken);

        var job = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(jobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(jobId);

        return _mapper.Map<IReadOnlyList<ArchiveItemDto>>(job.Items);
    }

    public async Task<ArchiveJobDto> FinalizeJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(jobId);

        if (job.FailedItemCount > 0)
        {
            var failedJob = await _archivalService.FailJobAsync(
                jobId,
                $"Job completed with {job.FailedItemCount} failed items.",
                cancellationToken);
            return _mapper.Map<ArchiveJobDto>(failedJob);
        }

        var completedJob = await _archivalService.CompleteJobAsync(jobId, cancellationToken);
        return _mapper.Map<ArchiveJobDto>(completedJob);
    }
}
