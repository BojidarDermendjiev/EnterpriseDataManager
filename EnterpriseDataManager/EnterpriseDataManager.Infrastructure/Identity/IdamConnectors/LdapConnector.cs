using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseDataManager.Infrastructure.Identity.IdamConnectors;

public class LdapConnector : IIdamConnector, IDisposable
{
    private readonly LdapOptions _options;
    private readonly ILogger<LdapConnector>? _logger;
    private LdapConnection? _connection;
    private bool _disposed;

    public string ProviderName => "LDAP";

    public LdapConnector(IOptions<LdapOptions> options, ILogger<LdapConnector>? logger = null)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Attempting LDAP authentication for user: {Username}", username);

            var userDn = await FindUserDnAsync(username, cancellationToken);
            if (userDn == null)
            {
                _logger?.LogWarning("User not found in LDAP: {Username}", username);
                return AuthenticationResult.Failure("User not found", AuthenticationErrorCode.UserNotFound);
            }

            // Try to bind with user credentials
            using var userConnection = CreateConnection();
            userConnection.Credential = new NetworkCredential(userDn, password);

            try
            {
                userConnection.Bind();
            }
            catch (LdapException ex) when (ex.ErrorCode == 49) // Invalid credentials
            {
                _logger?.LogWarning("Invalid credentials for user: {Username}", username);
                return AuthenticationResult.Failure("Invalid credentials", AuthenticationErrorCode.InvalidCredentials);
            }

            // Get user info after successful bind
            var userInfo = await GetUserInfoByDnAsync(userDn, cancellationToken);
            if (userInfo == null)
            {
                return AuthenticationResult.Failure("Failed to retrieve user information", AuthenticationErrorCode.Unknown);
            }

            if (!userInfo.IsEnabled)
            {
                _logger?.LogWarning("User account is disabled: {Username}", username);
                return AuthenticationResult.Failure("Account is disabled", AuthenticationErrorCode.UserDisabled);
            }

            if (userInfo.IsLocked)
            {
                _logger?.LogWarning("User account is locked: {Username}", username);
                return AuthenticationResult.Failure("Account is locked", AuthenticationErrorCode.UserLocked);
            }

            var groups = await GetUserGroupsAsync(userInfo.UserId, cancellationToken);
            var roles = MapGroupsToRoles(groups);
            var claims = BuildClaims(userInfo, roles, groups);

            _logger?.LogInformation("LDAP authentication successful for user: {Username}", username);

            return AuthenticationResult.Success(
                userId: userInfo.UserId,
                username: userInfo.Username,
                displayName: userInfo.DisplayName,
                email: userInfo.Email,
                roles: roles,
                groups: groups,
                claims: claims);
        }
        catch (LdapException ex)
        {
            _logger?.LogError(ex, "LDAP error during authentication for user: {Username}", username);
            return AuthenticationResult.Failure($"LDAP error: {ex.Message}", AuthenticationErrorCode.ConnectionFailed);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during LDAP authentication for user: {Username}", username);
            return AuthenticationResult.Failure($"Authentication failed: {ex.Message}", AuthenticationErrorCode.Unknown);
        }
    }

    public Task<AuthenticationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        // LDAP doesn't use tokens - this method is not applicable
        return Task.FromResult(AuthenticationResult.Failure(
            "Token validation not supported for LDAP",
            AuthenticationErrorCode.ConfigurationError));
    }

    public Task<UserInfo?> GetUserInfoAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureConnected();

            var searchFilter = string.Format(_options.UserSearchFilter, EscapeLdapFilterValue(userId));
            var searchRequest = new SearchRequest(
                _options.BaseDn,
                searchFilter,
                SearchScope.Subtree,
                GetUserAttributes());

            var response = (SearchResponse)_connection!.SendRequest(searchRequest);

            if (response.Entries.Count == 0)
            {
                return Task.FromResult<UserInfo?>(null);
            }

            return Task.FromResult<UserInfo?>(MapSearchEntryToUserInfo(response.Entries[0]));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving user info for: {UserId}", userId);
            return Task.FromResult<UserInfo?>(null);
        }
    }

    public async Task<IEnumerable<string>> GetUserGroupsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureConnected();

            var userDn = await FindUserDnAsync(userId, cancellationToken);
            if (userDn == null)
            {
                return Enumerable.Empty<string>();
            }

            var groups = new List<string>();

            // Search for groups that have this user as a member
            var groupSearchFilter = string.Format(_options.GroupMemberFilter, EscapeLdapFilterValue(userDn));
            var searchRequest = new SearchRequest(
                _options.GroupBaseDn ?? _options.BaseDn,
                groupSearchFilter,
                SearchScope.Subtree,
                new[] { "cn", "distinguishedName" });

            var response = (SearchResponse)_connection!.SendRequest(searchRequest);

            foreach (SearchResultEntry entry in response.Entries)
            {
                var groupName = GetAttributeValue(entry, "cn");
                if (!string.IsNullOrEmpty(groupName))
                {
                    groups.Add(groupName);
                }
            }

            // Also check memberOf attribute on the user
            var userSearchFilter = string.Format(_options.UserSearchFilter, EscapeLdapFilterValue(userId));
            var userRequest = new SearchRequest(
                _options.BaseDn,
                userSearchFilter,
                SearchScope.Subtree,
                new[] { "memberOf" });

            var userResponse = (SearchResponse)_connection!.SendRequest(userRequest);
            if (userResponse.Entries.Count > 0)
            {
                var memberOfAttr = userResponse.Entries[0].Attributes["memberOf"];
                if (memberOfAttr != null)
                {
                    foreach (var value in memberOfAttr.GetValues(typeof(string)))
                    {
                        var dn = value.ToString();
                        var cn = ExtractCnFromDn(dn);
                        if (!string.IsNullOrEmpty(cn) && !groups.Contains(cn))
                        {
                            groups.Add(cn);
                        }
                    }
                }
            }

            return groups;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving groups for user: {UserId}", userId);
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await AuthenticateAsync(username, password, cancellationToken);
        return result.IsSuccess;
    }

    public async Task<bool> IsUserLockedAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var userInfo = await GetUserInfoAsync(userId, cancellationToken);
        return userInfo?.IsLocked ?? false;
    }

    public async Task<bool> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userDn = await FindUserDnAsync(userId, cancellationToken);
            if (userDn == null)
            {
                _logger?.LogWarning("User not found for password change: {UserId}", userId);
                return false;
            }

            // Authenticate first to verify current password
            using var userConnection = CreateConnection();
            userConnection.Credential = new NetworkCredential(userDn, currentPassword);

            try
            {
                userConnection.Bind();
            }
            catch (LdapException)
            {
                _logger?.LogWarning("Current password validation failed for: {UserId}", userId);
                return false;
            }

            // Change password using the authenticated connection
            var passwordModification = new DirectoryAttributeModification
            {
                Operation = DirectoryAttributeOperation.Replace,
                Name = _options.PasswordAttribute
            };

            // For Active Directory, password needs to be in a specific format
            if (_options.IsActiveDirectory)
            {
                var newPasswordBytes = Encoding.Unicode.GetBytes($"\"{newPassword}\"");
                passwordModification.Add(newPasswordBytes);
            }
            else
            {
                passwordModification.Add(newPassword);
            }

            var modifyRequest = new ModifyRequest(userDn, passwordModification);
            userConnection.SendRequest(modifyRequest);

            _logger?.LogInformation("Password changed successfully for user: {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error changing password for user: {UserId}", userId);
            return false;
        }
    }

    private LdapConnection CreateConnection()
    {
        var identifier = new LdapDirectoryIdentifier(_options.Server, _options.Port);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            SessionOptions =
            {
                ProtocolVersion = 3,
                SecureSocketLayer = _options.UseSsl,
                ReferralChasing = ReferralChasingOptions.None
            }
        };

        if (_options.UseSsl || _options.UseStartTls)
        {
            connection.SessionOptions.VerifyServerCertificate =
                (conn, cert) => _options.AllowUntrustedCertificates || cert != null;
        }

        if (_options.UseStartTls)
        {
            connection.SessionOptions.StartTransportLayerSecurity(null);
        }

        return connection;
    }

    private void EnsureConnected()
    {
        if (_connection == null || _disposed)
        {
            _connection = CreateConnection();
            _connection.Credential = new NetworkCredential(_options.BindDn, _options.BindPassword);
            _connection.Bind();
        }
    }

    private Task<string?> FindUserDnAsync(string username, CancellationToken cancellationToken)
    {
        EnsureConnected();

        var searchFilter = string.Format(_options.UserSearchFilter, EscapeLdapFilterValue(username));
        var searchRequest = new SearchRequest(
            _options.BaseDn,
            searchFilter,
            SearchScope.Subtree,
            new[] { "distinguishedName" });

        var response = (SearchResponse)_connection!.SendRequest(searchRequest);

        if (response.Entries.Count == 0)
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(response.Entries[0].DistinguishedName);
    }

    private Task<UserInfo?> GetUserInfoByDnAsync(string userDn, CancellationToken cancellationToken)
    {
        EnsureConnected();

        var searchRequest = new SearchRequest(
            userDn,
            "(objectClass=*)",
            SearchScope.Base,
            GetUserAttributes());

        var response = (SearchResponse)_connection!.SendRequest(searchRequest);

        if (response.Entries.Count == 0)
        {
            return Task.FromResult<UserInfo?>(null);
        }

        return Task.FromResult<UserInfo?>(MapSearchEntryToUserInfo(response.Entries[0]));
    }

    private string[] GetUserAttributes()
    {
        return new[]
        {
            _options.UserIdAttribute,
            _options.UsernameAttribute,
            _options.EmailAttribute,
            _options.DisplayNameAttribute,
            _options.FirstNameAttribute,
            _options.LastNameAttribute,
            "department",
            "title",
            "telephoneNumber",
            "userAccountControl",
            "lockoutTime",
            "lastLogon",
            "pwdLastSet",
            "accountExpires",
            "memberOf"
        };
    }

    private UserInfo MapSearchEntryToUserInfo(SearchResultEntry entry)
    {
        var userAccountControl = GetAttributeValueAsInt(entry, "userAccountControl");
        var lockoutTime = GetAttributeValueAsLong(entry, "lockoutTime");
        var lastLogon = GetAttributeValueAsLong(entry, "lastLogon");
        var pwdLastSet = GetAttributeValueAsLong(entry, "pwdLastSet");
        var accountExpires = GetAttributeValueAsLong(entry, "accountExpires");

        var isDisabled = _options.IsActiveDirectory && (userAccountControl & 0x2) != 0;
        var isLocked = lockoutTime > 0;

        var groups = new List<string>();
        var memberOfAttr = entry.Attributes["memberOf"];
        if (memberOfAttr != null)
        {
            foreach (var value in memberOfAttr.GetValues(typeof(string)))
            {
                var cn = ExtractCnFromDn(value.ToString());
                if (!string.IsNullOrEmpty(cn))
                {
                    groups.Add(cn);
                }
            }
        }

        return new UserInfo
        {
            UserId = GetAttributeValue(entry, _options.UserIdAttribute) ?? entry.DistinguishedName,
            Username = GetAttributeValue(entry, _options.UsernameAttribute) ?? "",
            Email = GetAttributeValue(entry, _options.EmailAttribute),
            DisplayName = GetAttributeValue(entry, _options.DisplayNameAttribute),
            FirstName = GetAttributeValue(entry, _options.FirstNameAttribute),
            LastName = GetAttributeValue(entry, _options.LastNameAttribute),
            Department = GetAttributeValue(entry, "department"),
            Title = GetAttributeValue(entry, "title"),
            PhoneNumber = GetAttributeValue(entry, "telephoneNumber"),
            IsEnabled = !isDisabled,
            IsLocked = isLocked,
            LastLoginDate = FileTimeToDateTime(lastLogon),
            PasswordLastSet = FileTimeToDateTime(pwdLastSet),
            AccountExpirationDate = FileTimeToDateTime(accountExpires),
            Groups = groups
        };
    }

    private static string? GetAttributeValue(SearchResultEntry entry, string attributeName)
    {
        var attr = entry.Attributes[attributeName];
        if (attr == null || attr.Count == 0)
        {
            return null;
        }
        return attr[0]?.ToString();
    }

    private static int GetAttributeValueAsInt(SearchResultEntry entry, string attributeName)
    {
        var value = GetAttributeValue(entry, attributeName);
        return int.TryParse(value, out var result) ? result : 0;
    }

    private static long GetAttributeValueAsLong(SearchResultEntry entry, string attributeName)
    {
        var value = GetAttributeValue(entry, attributeName);
        return long.TryParse(value, out var result) ? result : 0;
    }

    private static DateTime? FileTimeToDateTime(long fileTime)
    {
        if (fileTime <= 0 || fileTime == long.MaxValue)
        {
            return null;
        }

        try
        {
            return DateTime.FromFileTimeUtc(fileTime);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractCnFromDn(string? dn)
    {
        if (string.IsNullOrEmpty(dn))
        {
            return null;
        }

        var parts = dn.Split(',');
        if (parts.Length > 0 && parts[0].StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
        {
            return parts[0].Substring(3);
        }

        return null;
    }

    private static string EscapeLdapFilterValue(string value)
    {
        var sb = new StringBuilder();
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\5c"); break;
                case '*': sb.Append("\\2a"); break;
                case '(': sb.Append("\\28"); break;
                case ')': sb.Append("\\29"); break;
                case '\0': sb.Append("\\00"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private IEnumerable<string> MapGroupsToRoles(IEnumerable<string> groups)
    {
        var roles = new List<string>();

        foreach (var group in groups)
        {
            if (_options.GroupToRoleMappings.TryGetValue(group, out var role))
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    private IEnumerable<Claim> BuildClaims(UserInfo userInfo, IEnumerable<string> roles, IEnumerable<string> groups)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userInfo.UserId),
            new Claim(ClaimTypes.Name, userInfo.Username)
        };

        if (!string.IsNullOrEmpty(userInfo.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, userInfo.Email));
        }

        if (!string.IsNullOrEmpty(userInfo.DisplayName))
        {
            claims.Add(new Claim("display_name", userInfo.DisplayName));
        }

        if (!string.IsNullOrEmpty(userInfo.FirstName))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, userInfo.FirstName));
        }

        if (!string.IsNullOrEmpty(userInfo.LastName))
        {
            claims.Add(new Claim(ClaimTypes.Surname, userInfo.LastName));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var group in groups)
        {
            claims.Add(new Claim("group", group));
        }

        return claims;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
        }
    }
}

public class LdapOptions
{
    public const string SectionName = "Identity:Ldap";

    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; } = false;
    public bool UseStartTls { get; set; } = false;
    public bool AllowUntrustedCertificates { get; set; } = false;
    public string BaseDn { get; set; } = string.Empty;
    public string? GroupBaseDn { get; set; }
    public string BindDn { get; set; } = string.Empty;
    public string BindPassword { get; set; } = string.Empty;
    public string UserSearchFilter { get; set; } = "(sAMAccountName={0})";
    public string GroupMemberFilter { get; set; } = "(&(objectClass=group)(member={0}))";
    public string UserIdAttribute { get; set; } = "objectGUID";
    public string UsernameAttribute { get; set; } = "sAMAccountName";
    public string EmailAttribute { get; set; } = "mail";
    public string DisplayNameAttribute { get; set; } = "displayName";
    public string FirstNameAttribute { get; set; } = "givenName";
    public string LastNameAttribute { get; set; } = "sn";
    public string PasswordAttribute { get; set; } = "unicodePwd";
    public bool IsActiveDirectory { get; set; } = true;
    public Dictionary<string, string> GroupToRoleMappings { get; set; } = new();
}
