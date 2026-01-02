namespace EnterpriseDataManager.Application.Handlers.ArchiveJobs;

using AutoMapper;
using EnterpriseDataManager.Application.Commands.ArchiveJobs;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;

public sealed class CreateArchiveJobCommandHandler : ICommandHandler<CreateArchiveJobCommand, ArchiveJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateArchiveJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchiveJobDto> Handle(CreateArchiveJobCommand request, CancellationToken cancellationToken)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(request.ArchivePlanId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(request.ArchivePlanId);

        var job = plan.CreateJob(request.Priority);

        if (request.ScheduledAt.HasValue)
        {
            job.Schedule(request.ScheduledAt);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetPath))
        {
            job.SetTargetPath(request.TargetPath);
        }

        await _unitOfWork.ArchiveJobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchiveJobDto>(job);
    }
}

public sealed class StartArchiveJobCommandHandler : ICommandHandler<StartArchiveJobCommand, ArchiveJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public StartArchiveJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchiveJobDto> Handle(StartArchiveJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(request.Id);

        job.Start();
        _unitOfWork.ArchiveJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchiveJobDto>(job);
    }
}

public sealed class CompleteArchiveJobCommandHandler : ICommandHandler<CompleteArchiveJobCommand, ArchiveJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CompleteArchiveJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchiveJobDto> Handle(CompleteArchiveJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(request.Id);

        job.Complete();
        _unitOfWork.ArchiveJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchiveJobDto>(job);
    }
}

public sealed class FailArchiveJobCommandHandler : ICommandHandler<FailArchiveJobCommand, ArchiveJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public FailArchiveJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchiveJobDto> Handle(FailArchiveJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(request.Id);

        job.Fail(request.Reason);
        _unitOfWork.ArchiveJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchiveJobDto>(job);
    }
}

public sealed class CancelArchiveJobCommandHandler : ICommandHandler<CancelArchiveJobCommand, ArchiveJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CancelArchiveJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchiveJobDto> Handle(CancelArchiveJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(request.Id);

        job.Cancel();
        _unitOfWork.ArchiveJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchiveJobDto>(job);
    }
}

public sealed class ScheduleArchiveJobCommandHandler : ICommandHandler<ScheduleArchiveJobCommand, ArchiveJobDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ScheduleArchiveJobCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchiveJobDto> Handle(ScheduleArchiveJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(request.Id);

        job.Schedule(request.ScheduledTime);
        _unitOfWork.ArchiveJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchiveJobDto>(job);
    }
}

public sealed class AddArchiveItemCommandHandler : ICommandHandler<AddArchiveItemCommand, ArchiveItemDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public AddArchiveItemCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchiveItemDto> Handle(AddArchiveItemCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(request.JobId);

        var item = job.AddItem(request.SourcePath, request.TargetPath, request.SizeBytes);

        _unitOfWork.ArchiveJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchiveItemDto>(item);
    }
}

public sealed class RecordArchiveItemSuccessCommandHandler : ICommandHandler<RecordArchiveItemSuccessCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public RecordArchiveItemSuccessCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RecordArchiveItemSuccessCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(request.JobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(request.JobId);

        var item = job.Items.FirstOrDefault(i => i.Id == request.ItemId)
            ?? throw EntityNotFoundException.ForArchiveItem(request.ItemId);

        job.RecordItemSuccess(item, request.Hash);
        _unitOfWork.ArchiveJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RecordArchiveItemFailureCommandHandler : ICommandHandler<RecordArchiveItemFailureCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public RecordArchiveItemFailureCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RecordArchiveItemFailureCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(request.JobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(request.JobId);

        var item = job.Items.FirstOrDefault(i => i.Id == request.ItemId)
            ?? throw EntityNotFoundException.ForArchiveItem(request.ItemId);

        job.RecordItemFailure(item, request.Error);
        _unitOfWork.ArchiveJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ProcessScheduledJobsCommandHandler : ICommandHandler<ProcessScheduledJobsCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public ProcessScheduledJobsCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ProcessScheduledJobsCommand request, CancellationToken cancellationToken)
    {
        var scheduledJobs = await _unitOfWork.ArchiveJobs.GetScheduledJobsDueAsync(DateTimeOffset.UtcNow, cancellationToken);

        foreach (var job in scheduledJobs)
        {
            job.Start();
            _unitOfWork.ArchiveJobs.Update(job);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
