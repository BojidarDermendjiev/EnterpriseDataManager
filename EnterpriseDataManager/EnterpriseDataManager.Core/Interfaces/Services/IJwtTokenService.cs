namespace EnterpriseDataManager.Core.Interfaces.Services;

public interface IJwtTokenService
{
    string GenerateToken(string userId, string email, IEnumerable<string> roles);
}
