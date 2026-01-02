namespace EnterpriseDataManager.Application.Services;

using AutoMapper;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;

public interface IStorageAppService
{
    Task<IReadOnlyList<StorageHealthDto>> CheckAllProvidersHealthAsync(CancellationToken cancellationToken = default);
    Task<StorageUsageDto> GetProviderUsageAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<bool> IsQuotaExceededAsync(Guid providerId, long additionalBytes, CancellationToken cancellationToken = default);
}

public sealed class StorageAppService : IStorageAppService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStorageService _storageService;
    private readonly IMapper _mapper;

    public StorageAppService(
        IUnitOfWork unitOfWork,
        IStorageService storageService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _storageService = storageService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<StorageHealthDto>> CheckAllProvidersHealthAsync(
        CancellationToken cancellationToken = default)
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

    public async Task<StorageUsageDto> GetProviderUsageAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(providerId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(providerId);

        var usage = await _storageService.GetUsageInfoAsync(providerId, cancellationToken);
        var dto = _mapper.Map<StorageUsageDto>(usage);
        return dto with { ProviderName = provider.Name };
    }

    public async Task<bool> IsQuotaExceededAsync(
        Guid providerId,
        long additionalBytes,
        CancellationToken cancellationToken = default)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(providerId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(providerId);

        if (!provider.QuotaBytes.HasValue)
        {
            return false;
        }

        var usage = await _storageService.GetUsageInfoAsync(providerId, cancellationToken);
        return (usage.UsedBytes + additionalBytes) > provider.QuotaBytes.Value;
    }
}
