namespace EnterpriseDataManager.Core.Entities;

using EnterpriseDataManager.Core.Entities.Common;

public class FilePermission : BaseEntity
{
    public string ArchiveItemId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public PermissionLevel Level { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? SignedUrlToken { get; set; }
    public DateTimeOffset? SignedUrlExpiresAt { get; set; }
}

public enum PermissionLevel { Read, Write, Admin }
