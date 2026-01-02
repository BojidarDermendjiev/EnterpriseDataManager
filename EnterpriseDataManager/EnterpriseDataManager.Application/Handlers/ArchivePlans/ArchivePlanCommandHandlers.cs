namespace EnterpriseDataManager.Application.Handlers.ArchivePlans;

using AutoMapper;
using EnterpriseDataManager.Application.Commands.ArchivePlans;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.ValueObjects;

public sealed class CreateArchivePlanCommandHandler : ICommandHandler<CreateArchivePlanCommand, ArchivePlanDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateArchivePlanCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchivePlanDto> Handle(CreateArchivePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = ArchivePlan.Create(request.Name, request.SourcePath, request.SecurityLevel);

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            plan.UpdateDetails(request.Name, request.Description);
        }

        if (!string.IsNullOrWhiteSpace(request.Schedule))
        {
            var schedule = CronSchedule.Create(request.Schedule, request.ScheduleDescription);
            plan.SetSchedule(schedule);
        }

        if (request.StorageProviderId.HasValue)
        {
            var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.StorageProviderId.Value, cancellationToken)
                ?? throw EntityNotFoundException.ForStorageProvider(request.StorageProviderId.Value);
            plan.SetStorageProvider(provider);
        }

        if (request.RetentionPolicyId.HasValue)
        {
            var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(request.RetentionPolicyId.Value, cancellationToken)
                ?? throw EntityNotFoundException.ForRetentionPolicy(request.RetentionPolicyId.Value);
            plan.SetRetentionPolicy(policy);
        }

        await _unitOfWork.ArchivePlans.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchivePlanDto>(plan);
    }
}

public sealed class UpdateArchivePlanCommandHandler : ICommandHandler<UpdateArchivePlanCommand, ArchivePlanDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdateArchivePlanCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchivePlanDto> Handle(UpdateArchivePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(request.Id);

        plan.UpdateDetails(request.Name, request.Description);

        if (request.SecurityLevel.HasValue)
        {
            plan.SetSecurityLevel(request.SecurityLevel.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Schedule))
        {
            var schedule = CronSchedule.Create(request.Schedule, request.ScheduleDescription);
            plan.SetSchedule(schedule);
        }
        else if (request.Schedule == string.Empty)
        {
            plan.ClearSchedule();
        }

        if (request.StorageProviderId.HasValue)
        {
            var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.StorageProviderId.Value, cancellationToken)
                ?? throw EntityNotFoundException.ForStorageProvider(request.StorageProviderId.Value);
            plan.SetStorageProvider(provider);
        }

        if (request.RetentionPolicyId.HasValue)
        {
            var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(request.RetentionPolicyId.Value, cancellationToken)
                ?? throw EntityNotFoundException.ForRetentionPolicy(request.RetentionPolicyId.Value);
            plan.SetRetentionPolicy(policy);
        }

        _unitOfWork.ArchivePlans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchivePlanDto>(plan);
    }
}

public sealed class DeleteArchivePlanCommandHandler : ICommandHandler<DeleteArchivePlanCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteArchivePlanCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteArchivePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(request.Id);

        await _unitOfWork.ArchivePlans.SoftDeleteAsync(request.Id, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ActivateArchivePlanCommandHandler : ICommandHandler<ActivateArchivePlanCommand, ArchivePlanDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ActivateArchivePlanCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchivePlanDto> Handle(ActivateArchivePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(request.Id);

        plan.Activate();
        _unitOfWork.ArchivePlans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchivePlanDto>(plan);
    }
}

public sealed class DeactivateArchivePlanCommandHandler : ICommandHandler<DeactivateArchivePlanCommand, ArchivePlanDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public DeactivateArchivePlanCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchivePlanDto> Handle(DeactivateArchivePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(request.Id);

        plan.Deactivate();
        _unitOfWork.ArchivePlans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchivePlanDto>(plan);
    }
}

public sealed class SetArchivePlanScheduleCommandHandler : ICommandHandler<SetArchivePlanScheduleCommand, ArchivePlanDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SetArchivePlanScheduleCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchivePlanDto> Handle(SetArchivePlanScheduleCommand request, CancellationToken cancellationToken)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(request.Id);

        var schedule = CronSchedule.Create(request.CronExpression, request.Description);
        plan.SetSchedule(schedule);

        _unitOfWork.ArchivePlans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchivePlanDto>(plan);
    }
}

public sealed class ClearArchivePlanScheduleCommandHandler : ICommandHandler<ClearArchivePlanScheduleCommand, ArchivePlanDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ClearArchivePlanScheduleCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArchivePlanDto> Handle(ClearArchivePlanScheduleCommand request, CancellationToken cancellationToken)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(request.Id);

        plan.ClearSchedule();
        _unitOfWork.ArchivePlans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ArchivePlanDto>(plan);
    }
}
