using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EnterpriseDataManager.Infrastructure.Identity.IdamConnectors;
using EnterpriseDataManager.Infrastructure.Identity.MfaProviders;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseDataManager.Infrastructure.Identity;

public interface IIdentityService
{
    Task<AuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        string? provider = null,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> CompleteMfaAsync(
        string mfaSessionToken,
        string code,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<UserInfo?> GetUserInfoAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsUserInRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default);

    Task<MfaSetupResult> SetupMfaAsync(
        string userId,
        string? displayName = null,
        CancellationToken cancellationToken = default);

    Task<MfaVerificationResult> VerifyMfaAsync(
        string userId,
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> DisableMfaAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsMfaEnabledAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task RevokeTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task RevokeAllUserTokensAsync(
        string userId,
        CancellationToken cancellationToken = default);

    IEnumerable<string> GetAvailableProviders();
}

public class IdentityService : IIdentityService
{
    private readonly IdentityOptions _options;
    private readonly IEnumerable<IIdamConnector> _connectors;
    private readonly IMfaProvider _mfaProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<IdentityService>? _logger;
    private readonly ConcurrentDictionary<string, MfaSession> _mfaSessions = new();
    private readonly ConcurrentDictionary<string, DateTime> _revokedTokens = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _userTokens = new();

    public IdentityService(
        IOptions<IdentityOptions> options,
        IEnumerable<IIdamConnector> connectors,
        IMfaProvider mfaProvider,
        IMemoryCache cache,
        ILogger<IdentityService>? logger = null)
    {
        _options = options.Value;
        _connectors = connectors;
        _mfaProvider = mfaProvider;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Authenticating user: {Username}", username);

            var connector = GetConnector(provider);
            if (connector == null)
            {
                return AuthenticationResult.Failure(
                    $"Identity provider '{provider ?? "default"}' not found",
                    AuthenticationErrorCode.ConfigurationError);
            }

            var result = await connector.AuthenticateAsync(username, password, cancellationToken);
            if (!result.IsSuccess)
            {
                return result;
            }

            // Check if MFA is required
            if (_options.RequireMfa || await _mfaProvider.IsEnabledAsync(result.UserId!, cancellationToken))
            {
                var mfaSessionToken = GenerateMfaSessionToken();
                var mfaSession = new MfaSession
                {
                    Token = mfaSessionToken,
                    UserId = result.UserId!,
                    Username = result.Username!,
                    Email = result.Email,
                    DisplayName = result.DisplayName,
                    Roles = result.Roles.ToList(),
                    Groups = result.Groups.ToList(),
                    Claims = result.Claims.ToList(),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(_options.MfaSessionTimeout)
                };

                _mfaSessions[mfaSessionToken] = mfaSession;

                _logger?.LogInformation("MFA required for user: {Username}", username);
                return AuthenticationResult.MfaRequired(mfaSessionToken);
            }

            // Generate tokens
            var (accessToken, refreshToken) = GenerateTokens(result);
            TrackUserToken(result.UserId!, accessToken);

            _logger?.LogInformation("Authentication successful for user: {Username}", username);

            return AuthenticationResult.Success(
                userId: result.UserId!,
                username: result.Username!,
                displayName: result.DisplayName,
                email: result.Email,
                token: accessToken,
                refreshToken: refreshToken,
                tokenExpiration: DateTime.UtcNow.Add(_options.TokenExpiration),
                roles: result.Roles,
                groups: result.Groups,
                claims: result.Claims);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error authenticating user: {Username}", username);
            return AuthenticationResult.Failure($"Authentication failed: {ex.Message}");
        }
    }

    public async Task<AuthenticationResult> CompleteMfaAsync(
        string mfaSessionToken,
        string code,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_mfaSessions.TryRemove(mfaSessionToken, out var session))
            {
                return AuthenticationResult.Failure(
                    "Invalid or expired MFA session",
                    AuthenticationErrorCode.TokenInvalid);
            }

            if (session.ExpiresAt < DateTime.UtcNow)
            {
                return AuthenticationResult.Failure(
                    "MFA session expired",
                    AuthenticationErrorCode.TokenExpired);
            }

            var verificationResult = await _mfaProvider.VerifyAsync(session.UserId, code, cancellationToken);
            if (!verificationResult.IsSuccess)
            {
                // Put session back for retry (unless locked out)
                if (!verificationResult.IsLockedOut)
                {
                    _mfaSessions[mfaSessionToken] = session;
                }

                return AuthenticationResult.Failure(
                    verificationResult.ErrorMessage ?? "MFA verification failed",
                    AuthenticationErrorCode.MfaFailed);
            }

            // Generate tokens
            var authResult = AuthenticationResult.Success(
                userId: session.UserId,
                username: session.Username,
                displayName: session.DisplayName,
                email: session.Email,
                roles: session.Roles,
                groups: session.Groups,
                claims: session.Claims);

            var (accessToken, refreshToken) = GenerateTokens(authResult);
            TrackUserToken(session.UserId, accessToken);

            _logger?.LogInformation("MFA verification successful for user: {Username}", session.Username);

            return AuthenticationResult.Success(
                userId: session.UserId,
                username: session.Username,
                displayName: session.DisplayName,
                email: session.Email,
                token: accessToken,
                refreshToken: refreshToken,
                tokenExpiration: DateTime.UtcNow.Add(_options.TokenExpiration),
                roles: session.Roles,
                groups: session.Groups,
                claims: session.Claims);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error completing MFA verification");
            return AuthenticationResult.Failure($"MFA verification failed: {ex.Message}");
        }
    }

    public Task<AuthenticationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if token is revoked
            if (_revokedTokens.ContainsKey(GetTokenHash(token)))
            {
                return Task.FromResult(AuthenticationResult.Failure("Token has been revoked", AuthenticationErrorCode.TokenInvalid));
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.TokenIssuer,
                ValidateAudience = true,
                ValidAudience = _options.TokenAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.TokenSigningKey)),
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var claims = principal.Claims.ToList();

            var userId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "";
            var username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? userId;
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var roles = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);

            return Task.FromResult(AuthenticationResult.Success(
                userId: userId,
                username: username,
                email: email,
                roles: roles,
                claims: claims));
        }
        catch (SecurityTokenExpiredException)
        {
            return Task.FromResult(AuthenticationResult.Failure("Token has expired", AuthenticationErrorCode.TokenExpired));
        }
        catch (SecurityTokenValidationException)
        {
            return Task.FromResult(AuthenticationResult.Failure("Token validation failed", AuthenticationErrorCode.TokenInvalid));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating token");
            return Task.FromResult(AuthenticationResult.Failure($"Token validation failed: {ex.Message}"));
        }
    }

    public async Task<AuthenticationResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"refresh_token:{GetTokenHash(refreshToken)}";
            if (!_cache.TryGetValue(cacheKey, out RefreshTokenData? tokenData) || tokenData == null)
            {
                return AuthenticationResult.Failure(
                    "Invalid or expired refresh token",
                    AuthenticationErrorCode.TokenInvalid);
            }

            // Remove old refresh token
            _cache.Remove(cacheKey);

            // Get current user info
            var userInfo = await GetUserInfoAsync(tokenData.UserId, cancellationToken);
            var roles = await GetUserRolesAsync(tokenData.UserId, cancellationToken);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, tokenData.UserId),
                new Claim(ClaimTypes.Name, tokenData.Username)
            };

            if (userInfo?.Email != null)
            {
                claims.Add(new Claim(ClaimTypes.Email, userInfo.Email));
            }

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var authResult = AuthenticationResult.Success(
                userId: tokenData.UserId,
                username: tokenData.Username,
                email: userInfo?.Email,
                roles: roles,
                claims: claims);

            var (newAccessToken, newRefreshToken) = GenerateTokens(authResult);
            TrackUserToken(tokenData.UserId, newAccessToken);

            return AuthenticationResult.Success(
                userId: tokenData.UserId,
                username: tokenData.Username,
                email: userInfo?.Email,
                token: newAccessToken,
                refreshToken: newRefreshToken,
                tokenExpiration: DateTime.UtcNow.Add(_options.TokenExpiration),
                roles: roles,
                claims: claims);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error refreshing token");
            return AuthenticationResult.Failure($"Token refresh failed: {ex.Message}");
        }
    }

    public async Task<UserInfo?> GetUserInfoAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        foreach (var connector in _connectors)
        {
            var userInfo = await connector.GetUserInfoAsync(userId, cancellationToken);
            if (userInfo != null)
            {
                return userInfo;
            }
        }

        return null;
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var roles = new HashSet<string>();

        foreach (var connector in _connectors)
        {
            var groups = await connector.GetUserGroupsAsync(userId, cancellationToken);
            foreach (var group in groups)
            {
                if (_options.GroupToRoleMappings.TryGetValue(group, out var role))
                {
                    roles.Add(role);
                }
            }
        }

        return roles;
    }

    public async Task<bool> IsUserInRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var roles = await GetUserRolesAsync(userId, cancellationToken);
        return roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<MfaSetupResult> SetupMfaAsync(
        string userId,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        return await _mfaProvider.SetupAsync(userId, displayName, cancellationToken);
    }

    public async Task<MfaVerificationResult> VerifyMfaAsync(
        string userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        return await _mfaProvider.VerifyAsync(userId, code, cancellationToken);
    }

    public async Task<bool> DisableMfaAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _mfaProvider.DisableAsync(userId, cancellationToken);
    }

    public async Task<bool> IsMfaEnabledAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _mfaProvider.IsEnabledAsync(userId, cancellationToken);
    }

    public async Task<bool> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        foreach (var connector in _connectors)
        {
            if (await connector.ChangePasswordAsync(userId, currentPassword, newPassword, cancellationToken))
            {
                _logger?.LogInformation("Password changed for user: {UserId}", userId);
                return true;
            }
        }

        return false;
    }

    public Task RevokeTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = GetTokenHash(token);
        _revokedTokens[tokenHash] = DateTime.UtcNow.Add(_options.TokenExpiration);

        // Cleanup expired revoked tokens
        CleanupRevokedTokens();

        _logger?.LogInformation("Token revoked");
        return Task.CompletedTask;
    }

    public Task RevokeAllUserTokensAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (_userTokens.TryRemove(userId, out var tokens))
        {
            foreach (var tokenHash in tokens)
            {
                _revokedTokens[tokenHash] = DateTime.UtcNow.Add(_options.TokenExpiration);
            }
        }

        CleanupRevokedTokens();

        _logger?.LogInformation("All tokens revoked for user: {UserId}", userId);
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetAvailableProviders()
    {
        return _connectors.Select(c => c.ProviderName);
    }

    private IIdamConnector? GetConnector(string? providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return _connectors.FirstOrDefault();
        }

        return _connectors.FirstOrDefault(c =>
            string.Equals(c.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
    }

    private (string accessToken, string refreshToken) GenerateTokens(AuthenticationResult authResult)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, authResult.UserId!),
            new Claim(ClaimTypes.Name, authResult.Username!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrEmpty(authResult.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, authResult.Email));
        }

        foreach (var role in authResult.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        claims.AddRange(authResult.Claims.Where(c =>
            c.Type != ClaimTypes.NameIdentifier &&
            c.Type != ClaimTypes.Name &&
            c.Type != ClaimTypes.Email &&
            c.Type != ClaimTypes.Role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.TokenSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.TokenIssuer,
            audience: _options.TokenAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_options.TokenExpiration),
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Generate refresh token
        var refreshTokenBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(refreshTokenBytes);
        var refreshToken = Convert.ToBase64String(refreshTokenBytes);

        // Store refresh token
        var refreshTokenData = new RefreshTokenData
        {
            UserId = authResult.UserId!,
            Username = authResult.Username!,
            CreatedAt = DateTime.UtcNow
        };

        _cache.Set(
            $"refresh_token:{GetTokenHash(refreshToken)}",
            refreshTokenData,
            _options.RefreshTokenExpiration);

        return (accessToken, refreshToken);
    }

    private static string GenerateMfaSessionToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string GetTokenHash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private void TrackUserToken(string userId, string accessToken)
    {
        var tokenHash = GetTokenHash(accessToken);
        _userTokens.AddOrUpdate(
            userId,
            _ => new HashSet<string> { tokenHash },
            (_, set) =>
            {
                set.Add(tokenHash);
                return set;
            });
    }

    private void CleanupRevokedTokens()
    {
        var now = DateTime.UtcNow;
        var expiredTokens = _revokedTokens.Where(kvp => kvp.Value < now).Select(kvp => kvp.Key).ToList();
        foreach (var tokenHash in expiredTokens)
        {
            _revokedTokens.TryRemove(tokenHash, out _);
        }
    }
}

public class IdentityOptions
{
    public const string SectionName = "Identity";

    public string TokenSigningKey { get; set; } = "DefaultSigningKey_ChangeInProduction_32chars!";
    public string TokenIssuer { get; set; } = "EnterpriseDataManager";
    public string TokenAudience { get; set; } = "EnterpriseDataManager";
    public TimeSpan TokenExpiration { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan RefreshTokenExpiration { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan MfaSessionTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public bool RequireMfa { get; set; } = false;
    public Dictionary<string, string> GroupToRoleMappings { get; set; } = new();
}

internal class MfaSession
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Groups { get; set; } = new();
    public List<Claim> Claims { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

internal class RefreshTokenData
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
