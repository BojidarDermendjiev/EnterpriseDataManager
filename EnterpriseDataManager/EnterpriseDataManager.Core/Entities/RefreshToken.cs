namespace EnterpriseDataManager.Core.Entities;

using EnterpriseDataManager.Core.Entities.Common;

public class RefreshToken : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string JwtId { get; set; } = string.Empty;
    public bool IsRevoked { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string CreatedByIp { get; set; } = string.Empty;
}
