namespace EnterpriseDataManager.Application.Handlers.RetentionPolicies;

using AutoMapper;
using EnterpriseDataManager.Application.Commands.RetentionPolicies;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;
using EnterpriseDataManager.Application.Queries.RetentionPolicies;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;

public sealed class CreateRetentionPolicyCommandHandler : ICommandHandler<CreateRetentionPolicyCommand, RetentionPolicyDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateRetentionPolicyCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RetentionPolicyDto> Handle(CreateRetentionPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = RetentionPolicy.CreateWithDays(request.Name, request.RetentionDays);

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            policy.UpdateDetails(request.Name, request.Description);
        }

        if (!string.IsNullOrWhiteSpace(request.Scope))
        {
            policy.SetScope(request.Scope);
        }

        await _unitOfWork.RetentionPolicies.AddAsync(policy, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RetentionPolicyDto>(policy);
    }
}

public sealed class UpdateRetentionPolicyCommandHandler : ICommandHandler<UpdateRetentionPolicyCommand, RetentionPolicyDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdateRetentionPolicyCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RetentionPolicyDto> Handle(UpdateRetentionPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForRetentionPolicy(request.Id);

        policy.UpdateDetails(request.Name, request.Description);

        if (request.RetentionDays.HasValue)
        {
            policy.SetRetentionPeriod(TimeSpan.FromDays(request.RetentionDays.Value));
        }

        policy.SetScope(request.Scope);

        _unitOfWork.RetentionPolicies.Update(policy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RetentionPolicyDto>(policy);
    }
}

public sealed class DeleteRetentionPolicyCommandHandler : ICommandHandler<DeleteRetentionPolicyCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRetentionPolicyCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteRetentionPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForRetentionPolicy(request.Id);

        await _unitOfWork.RetentionPolicies.SoftDeleteAsync(request.Id, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EnableLegalHoldCommandHandler : ICommandHandler<EnableLegalHoldCommand, RetentionPolicyDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public EnableLegalHoldCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RetentionPolicyDto> Handle(EnableLegalHoldCommand request, CancellationToken cancellationToken)
    {
        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForRetentionPolicy(request.Id);

        policy.EnableLegalHold();
        _unitOfWork.RetentionPolicies.Update(policy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RetentionPolicyDto>(policy);
    }
}

public sealed class DisableLegalHoldCommandHandler : ICommandHandler<DisableLegalHoldCommand, RetentionPolicyDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public DisableLegalHoldCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RetentionPolicyDto> Handle(DisableLegalHoldCommand request, CancellationToken cancellationToken)
    {
        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForRetentionPolicy(request.Id);

        policy.DisableLegalHold();
        _unitOfWork.RetentionPolicies.Update(policy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RetentionPolicyDto>(policy);
    }
}

public sealed class MakeRetentionPolicyImmutableCommandHandler : ICommandHandler<MakeRetentionPolicyImmutableCommand, RetentionPolicyDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public MakeRetentionPolicyImmutableCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RetentionPolicyDto> Handle(MakeRetentionPolicyImmutableCommand request, CancellationToken cancellationToken)
    {
        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForRetentionPolicy(request.Id);

        policy.MakeImmutable();
        _unitOfWork.RetentionPolicies.Update(policy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RetentionPolicyDto>(policy);
    }
}

public sealed class ProcessRetentionPoliciesCommandHandler : ICommandHandler<ProcessRetentionPoliciesCommand>
{
    private readonly IPolicyEngine _policyEngine;

    public ProcessRetentionPoliciesCommandHandler(IPolicyEngine policyEngine)
    {
        _policyEngine = policyEngine;
    }

    public async Task Handle(ProcessRetentionPoliciesCommand request, CancellationToken cancellationToken)
    {
        await _policyEngine.ProcessRetentionPoliciesAsync(cancellationToken);
    }
}

public sealed class GetRetentionPolicyByIdQueryHandler : IQueryHandler<GetRetentionPolicyByIdQuery, RetentionPolicyDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetRetentionPolicyByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<RetentionPolicyDto?> Handle(GetRetentionPolicyByIdQuery request, CancellationToken cancellationToken)
    {
        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(request.Id, cancellationToken);
        return policy is null ? null : _mapper.Map<RetentionPolicyDto>(policy);
    }
}

public sealed class GetAllRetentionPoliciesQueryHandler : IQueryHandler<GetAllRetentionPoliciesQuery, IReadOnlyList<RetentionPolicyDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetAllRetentionPoliciesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<RetentionPolicyDto>> Handle(GetAllRetentionPoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = await _unitOfWork.RetentionPolicies.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<RetentionPolicyDto>>(policies);
    }
}

public sealed class GetRetentionPolicySummariesQueryHandler : IQueryHandler<GetRetentionPolicySummariesQuery, IReadOnlyList<RetentionPolicySummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetRetentionPolicySummariesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<RetentionPolicySummaryDto>> Handle(GetRetentionPolicySummariesQuery request, CancellationToken cancellationToken)
    {
        var policies = await _unitOfWork.RetentionPolicies.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<RetentionPolicySummaryDto>>(policies);
    }
}

public sealed class GetRetentionPoliciesPagedQueryHandler : IQueryHandler<GetRetentionPoliciesPagedQuery, PagedResultDto<RetentionPolicySummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetRetentionPoliciesPagedQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<RetentionPolicySummaryDto>> Handle(GetRetentionPoliciesPagedQuery request, CancellationToken cancellationToken)
    {
        var allPolicies = await _unitOfWork.RetentionPolicies.GetAllAsync(cancellationToken);

        var query = allPolicies.AsQueryable();

        if (request.IsLegalHold.HasValue)
        {
            query = query.Where(p => p.IsLegalHold == request.IsLegalHold.Value);
        }

        if (request.IsImmutable.HasValue)
        {
            query = query.Where(p => p.IsImmutable == request.IsImmutable.Value);
        }

        var totalCount = query.Count();
        var items = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = _mapper.Map<List<RetentionPolicySummaryDto>>(items);

        return PagedResultDto<RetentionPolicySummaryDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

public sealed class GetLegalHoldPoliciesQueryHandler : IQueryHandler<GetLegalHoldPoliciesQuery, IReadOnlyList<RetentionPolicyDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetLegalHoldPoliciesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<RetentionPolicyDto>> Handle(GetLegalHoldPoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = await _unitOfWork.RetentionPolicies.GetLegalHoldPoliciesAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<RetentionPolicyDto>>(policies);
    }
}

public sealed class GetExpiredArchivesQueryHandler : IQueryHandler<GetExpiredArchivesQuery, IReadOnlyList<ArchiveJobDto>>
{
    private readonly IPolicyEngine _policyEngine;
    private readonly IMapper _mapper;

    public GetExpiredArchivesQueryHandler(IPolicyEngine policyEngine, IMapper mapper)
    {
        _policyEngine = policyEngine;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ArchiveJobDto>> Handle(GetExpiredArchivesQuery request, CancellationToken cancellationToken)
    {
        var jobs = await _policyEngine.GetExpiredArchivesAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<ArchiveJobDto>>(jobs);
    }
}

public sealed class GetArchivesApproachingExpiryQueryHandler : IQueryHandler<GetArchivesApproachingExpiryQuery, IReadOnlyList<ArchiveJobDto>>
{
    private readonly IPolicyEngine _policyEngine;
    private readonly IMapper _mapper;

    public GetArchivesApproachingExpiryQueryHandler(IPolicyEngine policyEngine, IMapper mapper)
    {
        _policyEngine = policyEngine;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ArchiveJobDto>> Handle(GetArchivesApproachingExpiryQuery request, CancellationToken cancellationToken)
    {
        var warningPeriod = TimeSpan.FromDays(request.WarningDays);
        var jobs = await _policyEngine.GetArchivesApproachingExpiryAsync(warningPeriod, cancellationToken);
        return _mapper.Map<IReadOnlyList<ArchiveJobDto>>(jobs);
    }
}
