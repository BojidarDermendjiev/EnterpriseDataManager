namespace EnterpriseDataManager.Core.Interfaces.Services;

using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Entities;

public interface IStorageService
{
    Task<StorageProvider> CreateProviderAsync(string name, StorageType type, CancellationToken cancellationToken = default);
    Task<StorageProvider> CreateLocalProviderAsync(string name, string rootPath, CancellationToken cancellationToken = default);
    Task<StorageProvider> CreateS3ProviderAsync(string name, string endpoint, string bucket, string? credentialsRef = null, CancellationToken cancellationToken = default);
    Task<StorageProvider> CreateAzureBlobProviderAsync(string name, string endpoint, string container, string? credentialsRef = null, CancellationToken cancellationToken = default);
    Task<StorageProvider> UpdateProviderAsync(Guid providerId, string name, string? description, CancellationToken cancellationToken = default);
    Task<StorageProvider> EnableProviderAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<StorageProvider> DisableProviderAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<StorageProvider> SetQuotaAsync(Guid providerId, long? quotaBytes, CancellationToken cancellationToken = default);
    Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageProvider>> GetEnabledProvidersAsync(CancellationToken cancellationToken = default);
    Task<StorageHealthStatus> CheckHealthAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<StorageUsageInfo> GetUsageInfoAsync(Guid providerId, CancellationToken cancellationToken = default);
}

public record StorageHealthStatus(
    Guid ProviderId,
    bool IsHealthy,
    bool IsReachable,
    TimeSpan? Latency,
    string? ErrorMessage,
    DateTimeOffset CheckedAt);

public record StorageUsageInfo(
    Guid ProviderId,
    long UsedBytes,
    long? QuotaBytes,
    double? UsagePercentage,
    int FileCount,
    DateTimeOffset CalculatedAt);

public interface IFileOperationService
{
    Task<Stream> ReadFileAsync(Guid storageProviderId, string path, CancellationToken cancellationToken = default);
    Task WriteFileAsync(Guid storageProviderId, string path, Stream content, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(Guid storageProviderId, string path, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(Guid storageProviderId, string path, CancellationToken cancellationToken = default);
    Task<FileMetadata> GetFileMetadataAsync(Guid storageProviderId, string path, CancellationToken cancellationToken = default);
    Task CopyFileAsync(Guid sourceProviderId, string sourcePath, Guid destinationProviderId, string destinationPath, CancellationToken cancellationToken = default);
    Task MoveFileAsync(Guid sourceProviderId, string sourcePath, Guid destinationProviderId, string destinationPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileMetadata>> ListFilesAsync(Guid storageProviderId, string path, bool recursive = false, CancellationToken cancellationToken = default);
    Task<string> CalculateHashAsync(Guid storageProviderId, string path, CancellationToken cancellationToken = default);
}

public record FileMetadata(
    string Path,
    string Name,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    string? Hash,
    bool IsDirectory);
