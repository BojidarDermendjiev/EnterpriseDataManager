namespace EnterpriseDataManager.Application.Services;

using AutoMapper;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;

public interface IRecoveryAppService
{
    Task<RecoveryJobDto> CreateAndStartRecoveryAsync(Guid archiveJobId, string destinationPath, CancellationToken cancellationToken = default);
    Task<RecoveryJobDto> FinalizeRecoveryAsync(Guid recoveryJobId, CancellationToken cancellationToken = default);
    Task<bool> ValidateAndRecoverAsync(Guid archiveJobId, string destinationPath, CancellationToken cancellationToken = default);
}

public sealed class RecoveryAppService : IRecoveryAppService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecoveryService _recoveryService;
    private readonly IMapper _mapper;

    public RecoveryAppService(
        IUnitOfWork unitOfWork,
        IRecoveryService recoveryService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _recoveryService = recoveryService;
        _mapper = mapper;
    }

    public async Task<RecoveryJobDto> CreateAndStartRecoveryAsync(
        Guid archiveJobId,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var job = await _recoveryService.CreateRecoveryJobAsync(archiveJobId, destinationPath, cancellationToken);
        var startedJob = await _recoveryService.StartRecoveryAsync(job.Id, cancellationToken);
        return _mapper.Map<RecoveryJobDto>(startedJob);
    }

    public async Task<RecoveryJobDto> FinalizeRecoveryAsync(
        Guid recoveryJobId,
        CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(recoveryJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForRecoveryJob(recoveryJobId);

        if (job.RecoveredItems < job.TotalItems)
        {
            var failedJob = await _recoveryService.FailRecoveryAsync(
                recoveryJobId,
                $"Recovery incomplete: {job.RecoveredItems}/{job.TotalItems} items recovered.",
                cancellationToken);
            return _mapper.Map<RecoveryJobDto>(failedJob);
        }

        var completedJob = await _recoveryService.CompleteRecoveryAsync(recoveryJobId, cancellationToken);
        return _mapper.Map<RecoveryJobDto>(completedJob);
    }

    public async Task<bool> ValidateAndRecoverAsync(
        Guid archiveJobId,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var isValid = await _recoveryService.ValidateArchiveIntegrityAsync(archiveJobId, cancellationToken);

        if (!isValid)
        {
            return false;
        }

        var job = await _recoveryService.CreateRecoveryJobAsync(archiveJobId, destinationPath, cancellationToken);
        await _recoveryService.StartRecoveryAsync(job.Id, cancellationToken);

        var archiveJob = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(archiveJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(archiveJobId);

        foreach (var item in archiveJob.Items.Where(i => i.Success == true))
        {
            await _recoveryService.RecoverItemAsync(job.Id, item.TargetPath, cancellationToken);
        }

        await _recoveryService.CompleteRecoveryAsync(job.Id, cancellationToken);
        return true;
    }
}
