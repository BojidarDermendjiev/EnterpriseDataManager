namespace EnterpriseDataManager.Infrastructure.Storage;

using EnterpriseDataManager.Core.Interfaces.Services;

public interface IStorageProviderAdapter
{
    Task<Stream> ReadAsync(string path, CancellationToken cancellationToken = default);
    Task WriteAsync(string path, Stream content, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
    Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileMetadata>> ListAsync(string path, bool recursive = false, CancellationToken cancellationToken = default);
    Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    Task<string> CalculateHashAsync(string path, CancellationToken cancellationToken = default);
    Task<StorageHealthStatus> CheckHealthAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<StorageUsageInfo> GetUsageAsync(Guid providerId, CancellationToken cancellationToken = default);
}
