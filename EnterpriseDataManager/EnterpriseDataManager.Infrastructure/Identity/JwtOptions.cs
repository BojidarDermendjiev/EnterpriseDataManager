namespace EnterpriseDataManager.Infrastructure.Identity;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "EnterpriseDataManager";
    public string Audience { get; set; } = "EnterpriseDataManager";
    public int ExpiryMinutes { get; set; } = 60;
}
