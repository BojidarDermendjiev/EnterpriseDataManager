using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseDataManager.Infrastructure.Identity.IdamConnectors;

public class OidcConnector : IIdamConnector
{
    private readonly OidcOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OidcConnector>? _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private OidcDiscoveryDocument? _discoveryDocument;
    private JsonWebKeySet? _jwks;

    public string ProviderName => "OIDC";

    public OidcConnector(
        IOptions<OidcOptions> options,
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<OidcConnector>? logger = null)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Attempting OIDC authentication for user: {Username}", username);

            await EnsureDiscoveryDocumentLoadedAsync(cancellationToken);

            // Use Resource Owner Password Credentials flow
            var tokenEndpoint = _discoveryDocument!.TokenEndpoint;
            var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["username"] = username,
                ["password"] = password,
                ["scope"] = string.Join(" ", _options.Scopes)
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, requestContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogWarning("OIDC token request failed: {StatusCode} - {Error}", response.StatusCode, errorContent);

                var errorResponse = JsonSerializer.Deserialize<OidcErrorResponse>(errorContent);
                return MapErrorToAuthResult(errorResponse);
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<OidcTokenResponse>(cancellationToken: cancellationToken);
            if (tokenResponse == null)
            {
                return AuthenticationResult.Failure("Invalid token response", AuthenticationErrorCode.Unknown);
            }

            // Validate and parse the ID token
            var validationResult = await ValidateAndParseTokenAsync(tokenResponse.IdToken ?? tokenResponse.AccessToken, cancellationToken);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            // Get user info if available
            UserInfo? userInfo = null;
            if (!string.IsNullOrEmpty(_discoveryDocument.UserInfoEndpoint))
            {
                userInfo = await GetUserInfoFromEndpointAsync(tokenResponse.AccessToken, cancellationToken);
            }

            var claims = validationResult.Claims.ToList();
            var userId = claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? username;
            var email = userInfo?.Email ?? claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var displayName = userInfo?.DisplayName ?? claims.FirstOrDefault(c => c.Type == "name")?.Value;

            var roles = claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value);
            var groups = claims.Where(c => c.Type == "groups" || c.Type == "group").Select(c => c.Value);

            _logger?.LogInformation("OIDC authentication successful for user: {Username}", username);

            return AuthenticationResult.Success(
                userId: userId,
                username: username,
                displayName: displayName,
                email: email,
                token: tokenResponse.AccessToken,
                refreshToken: tokenResponse.RefreshToken,
                tokenExpiration: DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                roles: roles,
                groups: groups,
                claims: claims);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error during OIDC authentication for user: {Username}", username);
            return AuthenticationResult.Failure($"Connection error: {ex.Message}", AuthenticationErrorCode.ConnectionFailed);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during OIDC authentication for user: {Username}", username);
            return AuthenticationResult.Failure($"Authentication failed: {ex.Message}", AuthenticationErrorCode.Unknown);
        }
    }

    public async Task<AuthenticationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return await ValidateAndParseTokenAsync(token, cancellationToken);
    }

    public Task<UserInfo?> GetUserInfoAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // OIDC typically doesn't provide a way to look up users by ID
        // This would require a separate user management API
        _logger?.LogWarning("GetUserInfoAsync by userId not supported in OIDC - requires active access token");
        return Task.FromResult<UserInfo?>(null);
    }

    public Task<IEnumerable<string>> GetUserGroupsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Groups are typically included in the token claims
        _logger?.LogWarning("GetUserGroupsAsync by userId not supported in OIDC - groups are available in token claims");
        return Task.FromResult(Enumerable.Empty<string>());
    }

    public async Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await AuthenticateAsync(username, password, cancellationToken);
        return result.IsSuccess;
    }

    public Task<bool> IsUserLockedAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // User lock status would be determined by the IdP
        // and reflected in authentication errors
        return Task.FromResult(false);
    }

    public Task<bool> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        // Password changes should be handled through the IdP's UI
        _logger?.LogWarning("ChangePasswordAsync not supported in OIDC - use IdP's password change flow");
        return Task.FromResult(false);
    }

    public async Task<AuthenticationResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureDiscoveryDocumentLoadedAsync(cancellationToken);

            var tokenEndpoint = _discoveryDocument!.TokenEndpoint;
            var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["refresh_token"] = refreshToken
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, requestContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogWarning("OIDC token refresh failed: {StatusCode}", response.StatusCode);

                var errorResponse = JsonSerializer.Deserialize<OidcErrorResponse>(errorContent);
                return MapErrorToAuthResult(errorResponse);
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<OidcTokenResponse>(cancellationToken: cancellationToken);
            if (tokenResponse == null)
            {
                return AuthenticationResult.Failure("Invalid token response", AuthenticationErrorCode.Unknown);
            }

            var validationResult = await ValidateAndParseTokenAsync(tokenResponse.IdToken ?? tokenResponse.AccessToken, cancellationToken);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            var claims = validationResult.Claims.ToList();
            var userId = claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "";
            var username = claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value ?? userId;

            return AuthenticationResult.Success(
                userId: userId,
                username: username,
                token: tokenResponse.AccessToken,
                refreshToken: tokenResponse.RefreshToken ?? refreshToken,
                tokenExpiration: DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                claims: claims);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error refreshing token");
            return AuthenticationResult.Failure($"Token refresh failed: {ex.Message}", AuthenticationErrorCode.Unknown);
        }
    }

    public async Task<string> GetAuthorizationUrlAsync(
        string state,
        string? nonce = null,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureDiscoveryDocumentLoadedAsync(cancellationToken);

        var authEndpoint = _discoveryDocument!.AuthorizationEndpoint;
        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["scope"] = string.Join(" ", _options.Scopes),
            ["redirect_uri"] = _options.RedirectUri,
            ["state"] = state
        };

        if (!string.IsNullOrEmpty(nonce))
        {
            queryParams["nonce"] = nonce;
        }

        if (!string.IsNullOrEmpty(codeChallenge))
        {
            queryParams["code_challenge"] = codeChallenge;
            queryParams["code_challenge_method"] = codeChallengeMethod ?? "S256";
        }

        var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{authEndpoint}?{queryString}";
    }

    public async Task<AuthenticationResult> ExchangeCodeAsync(
        string code,
        string? codeVerifier = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureDiscoveryDocumentLoadedAsync(cancellationToken);

            var tokenEndpoint = _discoveryDocument!.TokenEndpoint;
            var requestParams = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = _options.RedirectUri
            };

            if (!string.IsNullOrEmpty(codeVerifier))
            {
                requestParams["code_verifier"] = codeVerifier;
            }

            var requestContent = new FormUrlEncodedContent(requestParams);
            var response = await _httpClient.PostAsync(tokenEndpoint, requestContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogWarning("OIDC code exchange failed: {StatusCode}", response.StatusCode);

                var errorResponse = JsonSerializer.Deserialize<OidcErrorResponse>(errorContent);
                return MapErrorToAuthResult(errorResponse);
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<OidcTokenResponse>(cancellationToken: cancellationToken);
            if (tokenResponse == null)
            {
                return AuthenticationResult.Failure("Invalid token response", AuthenticationErrorCode.Unknown);
            }

            var validationResult = await ValidateAndParseTokenAsync(tokenResponse.IdToken ?? tokenResponse.AccessToken, cancellationToken);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            var claims = validationResult.Claims.ToList();
            var userId = claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "";
            var username = claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value ?? userId;
            var email = claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var displayName = claims.FirstOrDefault(c => c.Type == "name")?.Value;

            return AuthenticationResult.Success(
                userId: userId,
                username: username,
                displayName: displayName,
                email: email,
                token: tokenResponse.AccessToken,
                refreshToken: tokenResponse.RefreshToken,
                tokenExpiration: DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                claims: claims);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error exchanging authorization code");
            return AuthenticationResult.Failure($"Code exchange failed: {ex.Message}", AuthenticationErrorCode.Unknown);
        }
    }

    public static (string codeVerifier, string codeChallenge) GeneratePkceChallenge()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var codeVerifier = Base64UrlEncode(bytes);
        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeBytes);

        return (codeVerifier, codeChallenge);
    }

    private async Task EnsureDiscoveryDocumentLoadedAsync(CancellationToken cancellationToken)
    {
        if (_discoveryDocument != null)
        {
            return;
        }

        var cacheKey = $"oidc_discovery_{_options.Authority}";
        if (_cache.TryGetValue(cacheKey, out OidcDiscoveryDocument? cached))
        {
            _discoveryDocument = cached;
            return;
        }

        var discoveryUrl = _options.Authority.TrimEnd('/') + "/.well-known/openid-configuration";
        var response = await _httpClient.GetAsync(discoveryUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        _discoveryDocument = await response.Content.ReadFromJsonAsync<OidcDiscoveryDocument>(cancellationToken: cancellationToken);

        _cache.Set(cacheKey, _discoveryDocument, TimeSpan.FromHours(24));

        // Also load JWKS
        await LoadJwksAsync(cancellationToken);
    }

    private async Task LoadJwksAsync(CancellationToken cancellationToken)
    {
        if (_discoveryDocument?.JwksUri == null)
        {
            return;
        }

        var cacheKey = $"oidc_jwks_{_options.Authority}";
        if (_cache.TryGetValue(cacheKey, out JsonWebKeySet? cached))
        {
            _jwks = cached;
            return;
        }

        var response = await _httpClient.GetAsync(_discoveryDocument.JwksUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var jwksJson = await response.Content.ReadAsStringAsync(cancellationToken);
        _jwks = new JsonWebKeySet(jwksJson);

        _cache.Set(cacheKey, _jwks, TimeSpan.FromHours(24));
    }

    private async Task<AuthenticationResult> ValidateAndParseTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureDiscoveryDocumentLoadedAsync(cancellationToken);

            if (_jwks == null)
            {
                await LoadJwksAsync(cancellationToken);
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _discoveryDocument!.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.ClientId,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = _jwks?.Keys ?? Enumerable.Empty<JsonWebKey>(),
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var claims = principal.Claims.ToList();

            var userId = claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value ?? "";
            var username = claims.FirstOrDefault(c => c.Type == "preferred_username" || c.Type == ClaimTypes.Name)?.Value ?? userId;

            return AuthenticationResult.Success(
                userId: userId,
                username: username,
                claims: claims);
        }
        catch (SecurityTokenExpiredException)
        {
            return AuthenticationResult.Failure("Token has expired", AuthenticationErrorCode.TokenExpired);
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger?.LogWarning(ex, "Token validation failed");
            return AuthenticationResult.Failure("Token validation failed", AuthenticationErrorCode.TokenInvalid);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating token");
            return AuthenticationResult.Failure($"Token validation error: {ex.Message}", AuthenticationErrorCode.Unknown);
        }
    }

    private async Task<UserInfo?> GetUserInfoFromEndpointAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _discoveryDocument!.UserInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var userInfoResponse = await response.Content.ReadFromJsonAsync<OidcUserInfoResponse>(cancellationToken: cancellationToken);
            if (userInfoResponse == null)
            {
                return null;
            }

            return new UserInfo
            {
                UserId = userInfoResponse.Sub,
                Username = userInfoResponse.PreferredUsername ?? userInfoResponse.Sub,
                Email = userInfoResponse.Email,
                DisplayName = userInfoResponse.Name,
                FirstName = userInfoResponse.GivenName,
                LastName = userInfoResponse.FamilyName,
                PhoneNumber = userInfoResponse.PhoneNumber,
                Groups = userInfoResponse.Groups ?? Enumerable.Empty<string>()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get user info from endpoint");
            return null;
        }
    }

    private static AuthenticationResult MapErrorToAuthResult(OidcErrorResponse? error)
    {
        if (error == null)
        {
            return AuthenticationResult.Failure("Unknown error", AuthenticationErrorCode.Unknown);
        }

        var errorCode = error.Error switch
        {
            "invalid_grant" => AuthenticationErrorCode.InvalidCredentials,
            "invalid_client" => AuthenticationErrorCode.ConfigurationError,
            "unauthorized_client" => AuthenticationErrorCode.ConfigurationError,
            "access_denied" => AuthenticationErrorCode.UserDisabled,
            "invalid_scope" => AuthenticationErrorCode.ConfigurationError,
            _ => AuthenticationErrorCode.Unknown
        };

        return AuthenticationResult.Failure(
            error.ErrorDescription ?? error.Error,
            errorCode);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

public class OidcOptions
{
    public const string SectionName = "Identity:Oidc";

    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = new[] { "openid", "profile", "email" };
    public bool RequireHttpsMetadata { get; set; } = true;
    public bool SaveTokens { get; set; } = true;
    public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;
    public Dictionary<string, string> ClaimMappings { get; set; } = new();
}

internal class OidcDiscoveryDocument
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("userinfo_endpoint")]
    public string? UserInfoEndpoint { get; set; }

    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; set; }

    [JsonPropertyName("end_session_endpoint")]
    public string? EndSessionEndpoint { get; set; }

    [JsonPropertyName("scopes_supported")]
    public string[]? ScopesSupported { get; set; }

    [JsonPropertyName("response_types_supported")]
    public string[]? ResponseTypesSupported { get; set; }

    [JsonPropertyName("grant_types_supported")]
    public string[]? GrantTypesSupported { get; set; }

    [JsonPropertyName("claims_supported")]
    public string[]? ClaimsSupported { get; set; }
}

internal class OidcTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

internal class OidcErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

internal class OidcUserInfoResponse
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; set; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; set; }

    [JsonPropertyName("preferred_username")]
    public string? PreferredUsername { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; set; }

    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("groups")]
    public string[]? Groups { get; set; }
}
