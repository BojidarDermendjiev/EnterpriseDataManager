namespace EnterpriseDataManager.Application.Handlers.StorageProviders;

using AutoMapper;
using EnterpriseDataManager.Application.Commands.StorageProviders;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Application.DTOs.Common;
using EnterpriseDataManager.Application.Queries.StorageProviders;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;

public sealed class CreateLocalStorageProviderCommandHandler : ICommandHandler<CreateLocalStorageProviderCommand, StorageProviderDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateLocalStorageProviderCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageProviderDto> Handle(CreateLocalStorageProviderCommand request, CancellationToken cancellationToken)
    {
        var provider = StorageProvider.CreateLocal(request.Name, request.RootPath);

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            provider.UpdateDetails(request.Name, request.Description);
        }

        if (request.QuotaBytes.HasValue)
        {
            provider.SetQuota(request.QuotaBytes);
        }

        await _unitOfWork.StorageProviders.AddAsync(provider, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<StorageProviderDto>(provider);
    }
}

public sealed class CreateS3StorageProviderCommandHandler : ICommandHandler<CreateS3StorageProviderCommand, StorageProviderDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateS3StorageProviderCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageProviderDto> Handle(CreateS3StorageProviderCommand request, CancellationToken cancellationToken)
    {
        var provider = StorageProvider.CreateS3(request.Name, request.Endpoint, request.Bucket, request.CredentialsReference);

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            provider.UpdateDetails(request.Name, request.Description);
        }

        if (!string.IsNullOrWhiteSpace(request.RootPath))
        {
            provider.SetRootPath(request.RootPath);
        }

        if (request.QuotaBytes.HasValue)
        {
            provider.SetQuota(request.QuotaBytes);
        }

        await _unitOfWork.StorageProviders.AddAsync(provider, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<StorageProviderDto>(provider);
    }
}

public sealed class CreateAzureBlobStorageProviderCommandHandler : ICommandHandler<CreateAzureBlobStorageProviderCommand, StorageProviderDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateAzureBlobStorageProviderCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageProviderDto> Handle(CreateAzureBlobStorageProviderCommand request, CancellationToken cancellationToken)
    {
        var provider = StorageProvider.CreateAzureBlob(request.Name, request.Endpoint, request.Container, request.CredentialsReference);

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            provider.UpdateDetails(request.Name, request.Description);
        }

        if (!string.IsNullOrWhiteSpace(request.RootPath))
        {
            provider.SetRootPath(request.RootPath);
        }

        if (request.QuotaBytes.HasValue)
        {
            provider.SetQuota(request.QuotaBytes);
        }

        await _unitOfWork.StorageProviders.AddAsync(provider, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<StorageProviderDto>(provider);
    }
}

public sealed class UpdateStorageProviderCommandHandler : ICommandHandler<UpdateStorageProviderCommand, StorageProviderDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdateStorageProviderCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageProviderDto> Handle(UpdateStorageProviderCommand request, CancellationToken cancellationToken)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(request.Id);

        provider.UpdateDetails(request.Name, request.Description);

        if (!string.IsNullOrWhiteSpace(request.Endpoint))
        {
            provider.SetEndpoint(request.Endpoint);
        }

        if (!string.IsNullOrWhiteSpace(request.BucketOrContainer))
        {
            provider.SetBucketOrContainer(request.BucketOrContainer);
        }

        provider.SetRootPath(request.RootPath);
        provider.SetCredentialsReference(request.CredentialsReference);

        if (request.QuotaBytes.HasValue)
        {
            provider.SetQuota(request.QuotaBytes);
        }

        _unitOfWork.StorageProviders.Update(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<StorageProviderDto>(provider);
    }
}

public sealed class DeleteStorageProviderCommandHandler : ICommandHandler<DeleteStorageProviderCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteStorageProviderCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteStorageProviderCommand request, CancellationToken cancellationToken)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(request.Id);

        await _unitOfWork.StorageProviders.SoftDeleteAsync(request.Id, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EnableStorageProviderCommandHandler : ICommandHandler<EnableStorageProviderCommand, StorageProviderDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public EnableStorageProviderCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageProviderDto> Handle(EnableStorageProviderCommand request, CancellationToken cancellationToken)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(request.Id);

        provider.Enable();
        _unitOfWork.StorageProviders.Update(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<StorageProviderDto>(provider);
    }
}

public sealed class DisableStorageProviderCommandHandler : ICommandHandler<DisableStorageProviderCommand, StorageProviderDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public DisableStorageProviderCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageProviderDto> Handle(DisableStorageProviderCommand request, CancellationToken cancellationToken)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(request.Id);

        provider.Disable();
        _unitOfWork.StorageProviders.Update(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<StorageProviderDto>(provider);
    }
}

public sealed class SetStorageQuotaCommandHandler : ICommandHandler<SetStorageQuotaCommand, StorageProviderDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SetStorageQuotaCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageProviderDto> Handle(SetStorageQuotaCommand request, CancellationToken cancellationToken)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(request.Id);

        provider.SetQuota(request.QuotaBytes);
        _unitOfWork.StorageProviders.Update(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<StorageProviderDto>(provider);
    }
}

public sealed class MakeStorageImmutableCommandHandler : ICommandHandler<MakeStorageImmutableCommand, StorageProviderDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public MakeStorageImmutableCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageProviderDto> Handle(MakeStorageImmutableCommand request, CancellationToken cancellationToken)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.Id, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(request.Id);

        provider.MakeImmutable();
        _unitOfWork.StorageProviders.Update(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<StorageProviderDto>(provider);
    }
}

public sealed class GetStorageProviderByIdQueryHandler : IQueryHandler<GetStorageProviderByIdQuery, StorageProviderDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetStorageProviderByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageProviderDto?> Handle(GetStorageProviderByIdQuery request, CancellationToken cancellationToken)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.Id, cancellationToken);
        return provider is null ? null : _mapper.Map<StorageProviderDto>(provider);
    }
}

public sealed class GetAllStorageProvidersQueryHandler : IQueryHandler<GetAllStorageProvidersQuery, IReadOnlyList<StorageProviderDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetAllStorageProvidersQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<StorageProviderDto>> Handle(GetAllStorageProvidersQuery request, CancellationToken cancellationToken)
    {
        var providers = await _unitOfWork.StorageProviders.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<StorageProviderDto>>(providers);
    }
}

public sealed class GetEnabledStorageProvidersQueryHandler : IQueryHandler<GetEnabledStorageProvidersQuery, IReadOnlyList<StorageProviderDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetEnabledStorageProvidersQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<StorageProviderDto>> Handle(GetEnabledStorageProvidersQuery request, CancellationToken cancellationToken)
    {
        var providers = await _unitOfWork.StorageProviders.GetEnabledProvidersAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<StorageProviderDto>>(providers);
    }
}

public sealed class GetStorageProviderSummariesQueryHandler : IQueryHandler<GetStorageProviderSummariesQuery, IReadOnlyList<StorageProviderSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetStorageProviderSummariesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<StorageProviderSummaryDto>> Handle(GetStorageProviderSummariesQuery request, CancellationToken cancellationToken)
    {
        var providers = await _unitOfWork.StorageProviders.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<StorageProviderSummaryDto>>(providers);
    }
}

public sealed class GetStorageProvidersPagedQueryHandler : IQueryHandler<GetStorageProvidersPagedQuery, PagedResultDto<StorageProviderSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetStorageProvidersPagedQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<StorageProviderSummaryDto>> Handle(GetStorageProvidersPagedQuery request, CancellationToken cancellationToken)
    {
        var allProviders = await _unitOfWork.StorageProviders.GetAllAsync(cancellationToken);

        var query = allProviders.AsQueryable();

        if (request.Type.HasValue)
        {
            query = query.Where(p => p.Type == request.Type.Value);
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(p => p.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.Count();
        var items = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = _mapper.Map<List<StorageProviderSummaryDto>>(items);

        return PagedResultDto<StorageProviderSummaryDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

public sealed class GetStorageHealthQueryHandler : IQueryHandler<GetStorageHealthQuery, StorageHealthDto>
{
    private readonly IStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetStorageHealthQueryHandler(IStorageService storageService, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _storageService = storageService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageHealthDto> Handle(GetStorageHealthQuery request, CancellationToken cancellationToken)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.ProviderId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(request.ProviderId);

        var health = await _storageService.CheckHealthAsync(request.ProviderId, cancellationToken);

        var dto = _mapper.Map<StorageHealthDto>(health);
        return dto with { ProviderName = provider.Name };
    }
}

public sealed class GetStorageUsageQueryHandler : IQueryHandler<GetStorageUsageQuery, StorageUsageDto>
{
    private readonly IStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetStorageUsageQueryHandler(IStorageService storageService, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _storageService = storageService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<StorageUsageDto> Handle(GetStorageUsageQuery request, CancellationToken cancellationToken)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(request.ProviderId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(request.ProviderId);

        var usage = await _storageService.GetUsageInfoAsync(request.ProviderId, cancellationToken);

        var dto = _mapper.Map<StorageUsageDto>(usage);
        return dto with { ProviderName = provider.Name };
    }
}

public sealed class GetAllStorageHealthQueryHandler : IQueryHandler<GetAllStorageHealthQuery, IReadOnlyList<StorageHealthDto>>
{
    private readonly IStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetAllStorageHealthQueryHandler(IStorageService storageService, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _storageService = storageService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<StorageHealthDto>> Handle(GetAllStorageHealthQuery request, CancellationToken cancellationToken)
    {
        var providers = await _unitOfWork.StorageProviders.GetEnabledProvidersAsync(cancellationToken);
        var results = new List<StorageHealthDto>();

        foreach (var provider in providers)
        {
            var health = await _storageService.CheckHealthAsync(provider.Id, cancellationToken);
            var dto = _mapper.Map<StorageHealthDto>(health);
            results.Add(dto with { ProviderName = provider.Name });
        }

        return results;
    }
}
