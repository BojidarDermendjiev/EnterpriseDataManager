namespace EnterpriseDataManager.Core.Interfaces.Services;

using EnterpriseDataManager.Core.Entities;

public interface IShareService
{
    Task<FilePermission> GrantPermissionAsync(string archiveItemId, string userId, PermissionLevel level, DateTimeOffset? expiresAt, CancellationToken ct = default);
    Task RevokePermissionAsync(string archiveItemId, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<FilePermission>> GetPermissionsAsync(string archiveItemId, CancellationToken ct = default);
    Task<string> GenerateSignedUrlAsync(string archiveItemId, string userId, TimeSpan validity, CancellationToken ct = default);
    Task<bool> ValidateSignedUrlAsync(string token, CancellationToken ct = default);
}
