using System.Security.Claims;

namespace EnterpriseDataManager.Infrastructure.Identity.IdamConnectors;

public interface IIdamConnector
{
    string ProviderName { get; }

    Task<AuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<UserInfo?> GetUserInfoAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetUserGroupsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<bool> IsUserLockedAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public class AuthenticationResult
{
    public bool IsSuccess { get; init; }
    public string? UserId { get; init; }
    public string? Username { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public string? Token { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime? TokenExpiration { get; init; }
    public IEnumerable<string> Roles { get; init; } = Enumerable.Empty<string>();
    public IEnumerable<string> Groups { get; init; } = Enumerable.Empty<string>();
    public IEnumerable<Claim> Claims { get; init; } = Enumerable.Empty<Claim>();
    public string? ErrorMessage { get; init; }
    public AuthenticationErrorCode? ErrorCode { get; init; }
    public bool RequiresMfa { get; init; }
    public string? MfaSessionToken { get; init; }

    public static AuthenticationResult Success(
        string userId,
        string username,
        string? displayName = null,
        string? email = null,
        string? token = null,
        string? refreshToken = null,
        DateTime? tokenExpiration = null,
        IEnumerable<string>? roles = null,
        IEnumerable<string>? groups = null,
        IEnumerable<Claim>? claims = null)
    {
        return new AuthenticationResult
        {
            IsSuccess = true,
            UserId = userId,
            Username = username,
            DisplayName = displayName,
            Email = email,
            Token = token,
            RefreshToken = refreshToken,
            TokenExpiration = tokenExpiration,
            Roles = roles ?? Enumerable.Empty<string>(),
            Groups = groups ?? Enumerable.Empty<string>(),
            Claims = claims ?? Enumerable.Empty<Claim>()
        };
    }

    public static AuthenticationResult Failure(
        string errorMessage,
        AuthenticationErrorCode errorCode = AuthenticationErrorCode.Unknown)
    {
        return new AuthenticationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
    }

    public static AuthenticationResult MfaRequired(string mfaSessionToken)
    {
        return new AuthenticationResult
        {
            IsSuccess = false,
            RequiresMfa = true,
            MfaSessionToken = mfaSessionToken,
            ErrorCode = AuthenticationErrorCode.MfaRequired
        };
    }
}

public enum AuthenticationErrorCode
{
    Unknown = 0,
    InvalidCredentials = 1,
    UserNotFound = 2,
    UserLocked = 3,
    UserDisabled = 4,
    PasswordExpired = 5,
    MfaRequired = 6,
    MfaFailed = 7,
    TokenExpired = 8,
    TokenInvalid = 9,
    ConnectionFailed = 10,
    ConfigurationError = 11
}

public class UserInfo
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Department { get; init; }
    public string? Title { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsLocked { get; init; }
    public DateTime? LastLoginDate { get; init; }
    public DateTime? PasswordLastSet { get; init; }
    public DateTime? AccountExpirationDate { get; init; }
    public IEnumerable<string> Groups { get; init; } = Enumerable.Empty<string>();
    public IEnumerable<string> Roles { get; init; } = Enumerable.Empty<string>();
    public IDictionary<string, object> AdditionalAttributes { get; init; } = new Dictionary<string, object>();
}
