namespace EnterpriseDataManager.Core.Interfaces.Services;

public interface IJwtTokenService
{
    string GenerateToken(string userId, string email, IEnumerable<string> roles);
    TokenWithJti GenerateTokenWithJti(string userId, string email, IEnumerable<string> roles);
}

public record TokenWithJti(string Token, string Jti);
