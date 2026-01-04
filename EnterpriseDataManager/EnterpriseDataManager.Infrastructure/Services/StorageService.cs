namespace EnterpriseDataManager.Infrastructure.Services;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

public sealed class StorageService : IStorageService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StorageService>? _logger;
    private readonly LocalFilesystemProvider? _localProvider;
    private readonly S3CompatibleProvider? _s3Provider;
    private readonly AzureBlobProvider? _azureProvider;

    public StorageService(
        IUnitOfWork unitOfWork,
        ILogger<StorageService>? logger = null,
        LocalFilesystemProvider? localProvider = null,
        S3CompatibleProvider? s3Provider = null,
        AzureBlobProvider? azureProvider = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _localProvider = localProvider;
        _s3Provider = s3Provider;
        _azureProvider = azureProvider;
    }

    public async Task<StorageProvider> CreateProviderAsync(string name, StorageType type, CancellationToken cancellationToken = default)
    {
        var provider = StorageProvider.Create(name, type);
        await _unitOfWork.StorageProviders.AddAsync(provider, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Created storage provider {ProviderId} of type {Type}", provider.Id, type);
        return provider;
    }

    public async Task<StorageProvider> CreateLocalProviderAsync(string name, string rootPath, CancellationToken cancellationToken = default)
    {
        var provider = StorageProvider.CreateLocal(name, rootPath);
        await _unitOfWork.StorageProviders.AddAsync(provider, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return provider;
    }

    public async Task<StorageProvider> CreateS3ProviderAsync(string name, string endpoint, string bucket, string? credentialsRef = null, CancellationToken cancellationToken = default)
    {
        var provider = StorageProvider.CreateS3(name, endpoint, bucket, credentialsRef);
        await _unitOfWork.StorageProviders.AddAsync(provider, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return provider;
    }

    public async Task<StorageProvider> CreateAzureBlobProviderAsync(string name, string endpoint, string container, string? credentialsRef = null, CancellationToken cancellationToken = default)
    {
        var provider = StorageProvider.CreateAzureBlob(name, endpoint, container, credentialsRef);
        await _unitOfWork.StorageProviders.AddAsync(provider, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return provider;
    }

    public async Task<StorageProvider> UpdateProviderAsync(Guid providerId, string name, string? description, CancellationToken cancellationToken = default)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(providerId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(providerId);

        provider.UpdateDetails(name, description);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return provider;
    }

    public async Task<StorageProvider> EnableProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(providerId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(providerId);

        provider.Enable();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return provider;
    }

    public async Task<StorageProvider> DisableProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(providerId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(providerId);

        provider.Disable();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return provider;
    }

    public async Task<StorageProvider> SetQuotaAsync(Guid providerId, long? quotaBytes, CancellationToken cancellationToken = default)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(providerId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(providerId);

        provider.SetQuota(quotaBytes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return provider;
    }

    public async Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(providerId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(providerId);

        _unitOfWork.StorageProviders.Remove(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StorageProvider>> GetEnabledProvidersAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.StorageProviders.GetEnabledProvidersAsync(cancellationToken);
    }

    public async Task<StorageHealthStatus> CheckHealthAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(providerId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(providerId);

        var adapter = GetProviderAdapter(provider);
        if (adapter == null)
        {
            return new StorageHealthStatus(
                providerId,
                IsHealthy: false,
                IsReachable: false,
                Latency: null,
                ErrorMessage: $"No adapter available for storage type {provider.Type}",
                CheckedAt: DateTimeOffset.UtcNow);
        }

        return await adapter.CheckHealthAsync(providerId, cancellationToken);
    }

    public async Task<StorageUsageInfo> GetUsageInfoAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(providerId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(providerId);

        var adapter = GetProviderAdapter(provider);
        if (adapter == null)
        {
            return new StorageUsageInfo(
                providerId,
                UsedBytes: 0,
                QuotaBytes: provider.QuotaBytes,
                UsagePercentage: null,
                FileCount: 0,
                CalculatedAt: DateTimeOffset.UtcNow);
        }

        return await adapter.GetUsageAsync(providerId, cancellationToken);
    }

    private IStorageProviderAdapter? GetProviderAdapter(StorageProvider provider)
    {
        return provider.Type switch
        {
            StorageType.Local => _localProvider,
            StorageType.S3Compatible => _s3Provider,
            StorageType.AzureBlob => _azureProvider,
            _ => null
        };
    }
}
