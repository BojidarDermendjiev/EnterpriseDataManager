namespace EnterpriseDataManager.Data;

public class MfaState
{
    public string UserId { get; set; } = string.Empty;
    public string StateJson { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
