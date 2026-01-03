using System.Security.Claims;
using EnterpriseDataManager.Infrastructure.Identity;
using EnterpriseDataManager.Infrastructure.Identity.IdamConnectors;
using EnterpriseDataManager.Infrastructure.Identity.MfaProviders;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;

namespace EnterpriseDataManager.UnitTests.Identity;

public class IdentityServiceTests
{
    private readonly Mock<IIdamConnector> _mockConnector;
    private readonly Mock<IMfaProvider> _mockMfaProvider;
    private readonly IMemoryCache _cache;
    private readonly IdentityOptions _options;
    private readonly IdentityService _service;

    public IdentityServiceTests()
    {
        _mockConnector = new Mock<IIdamConnector>();
        _mockConnector.Setup(c => c.ProviderName).Returns("Test");

        _mockMfaProvider = new Mock<IMfaProvider>();
        _mockMfaProvider.Setup(m => m.ProviderName).Returns("TOTP");

        _cache = new MemoryCache(new MemoryCacheOptions());

        _options = new IdentityOptions
        {
            TokenSigningKey = "TestSigningKey_MustBe32CharsLong!!",
            TokenIssuer = "TestIssuer",
            TokenAudience = "TestAudience",
            TokenExpiration = TimeSpan.FromHours(1),
            RefreshTokenExpiration = TimeSpan.FromDays(7),
            MfaSessionTimeout = TimeSpan.FromMinutes(5),
            RequireMfa = false
        };

        _service = new IdentityService(
            Options.Create(_options),
            new[] { _mockConnector.Object },
            _mockMfaProvider.Object,
            _cache);
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldReturnSuccess_WhenCredentialsAreValid()
    {
        // Arrange
        var username = "testuser";
        var password = "password123";
        var authResult = AuthenticationResult.Success(
            userId: "user-123",
            username: username,
            email: "test@example.com",
            roles: new[] { "Admin" });

        _mockConnector.Setup(c => c.AuthenticateAsync(username, password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _mockMfaProvider.Setup(m => m.IsEnabledAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.AuthenticateAsync(username, password);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UserId.Should().Be("user-123");
        result.Username.Should().Be(username);
        result.Token.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.TokenExpiration.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldReturnFailure_WhenCredentialsAreInvalid()
    {
        // Arrange
        var username = "testuser";
        var password = "wrongpassword";

        _mockConnector.Setup(c => c.AuthenticateAsync(username, password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthenticationResult.Failure("Invalid credentials", AuthenticationErrorCode.InvalidCredentials));

        // Act
        var result = await _service.AuthenticateAsync(username, password);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthenticationErrorCode.InvalidCredentials);
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldRequireMfa_WhenMfaIsEnabled()
    {
        // Arrange
        var username = "testuser";
        var password = "password123";
        var authResult = AuthenticationResult.Success(
            userId: "user-123",
            username: username);

        _mockConnector.Setup(c => c.AuthenticateAsync(username, password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _mockMfaProvider.Setup(m => m.IsEnabledAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.AuthenticateAsync(username, password);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.RequiresMfa.Should().BeTrue();
        result.MfaSessionToken.Should().NotBeNullOrEmpty();
        result.ErrorCode.Should().Be(AuthenticationErrorCode.MfaRequired);
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldRequireMfa_WhenRequireMfaOptionIsTrue()
    {
        // Arrange
        var optionsWithMfa = new IdentityOptions
        {
            TokenSigningKey = "TestSigningKey_MustBe32CharsLong!!",
            TokenIssuer = "TestIssuer",
            TokenAudience = "TestAudience",
            RequireMfa = true
        };

        var service = new IdentityService(
            Options.Create(optionsWithMfa),
            new[] { _mockConnector.Object },
            _mockMfaProvider.Object,
            _cache);

        var username = "testuser";
        var password = "password123";
        var authResult = AuthenticationResult.Success(
            userId: "user-123",
            username: username);

        _mockConnector.Setup(c => c.AuthenticateAsync(username, password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        // Act
        var result = await service.AuthenticateAsync(username, password);

        // Assert
        result.RequiresMfa.Should().BeTrue();
        result.MfaSessionToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteMfaAsync_ShouldReturnSuccess_WhenMfaCodeIsValid()
    {
        // Arrange
        var username = "testuser";
        var password = "password123";
        var authResult = AuthenticationResult.Success(
            userId: "user-123",
            username: username,
            email: "test@example.com");

        _mockConnector.Setup(c => c.AuthenticateAsync(username, password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _mockMfaProvider.Setup(m => m.IsEnabledAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockMfaProvider.Setup(m => m.VerifyAsync("user-123", "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MfaVerificationResult.Success());

        // First authenticate to get MFA session token
        var mfaResult = await _service.AuthenticateAsync(username, password);

        // Act
        var result = await _service.CompleteMfaAsync(mfaResult.MfaSessionToken!, "123456");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Token.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteMfaAsync_ShouldReturnFailure_WhenMfaCodeIsInvalid()
    {
        // Arrange
        var username = "testuser";
        var password = "password123";
        var authResult = AuthenticationResult.Success(
            userId: "user-123",
            username: username);

        _mockConnector.Setup(c => c.AuthenticateAsync(username, password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _mockMfaProvider.Setup(m => m.IsEnabledAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockMfaProvider.Setup(m => m.VerifyAsync("user-123", "000000", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MfaVerificationResult.Failure("Invalid code", MfaVerificationErrorCode.InvalidCode, 2));

        var mfaResult = await _service.AuthenticateAsync(username, password);

        // Act
        var result = await _service.CompleteMfaAsync(mfaResult.MfaSessionToken!, "000000");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthenticationErrorCode.MfaFailed);
    }

    [Fact]
    public async Task CompleteMfaAsync_ShouldReturnFailure_WhenSessionTokenIsInvalid()
    {
        // Act
        var result = await _service.CompleteMfaAsync("invalid-session-token", "123456");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthenticationErrorCode.TokenInvalid);
    }

    [Fact]
    public async Task ValidateTokenAsync_ShouldReturnSuccess_WhenTokenIsValid()
    {
        // Arrange
        var username = "testuser";
        var password = "password123";
        var authResult = AuthenticationResult.Success(
            userId: "user-123",
            username: username,
            roles: new[] { "Admin" });

        _mockConnector.Setup(c => c.AuthenticateAsync(username, password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _mockMfaProvider.Setup(m => m.IsEnabledAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var loginResult = await _service.AuthenticateAsync(username, password);

        // Act
        var result = await _service.ValidateTokenAsync(loginResult.Token!);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UserId.Should().Be("user-123");
        result.Username.Should().Be(username);
        result.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task ValidateTokenAsync_ShouldReturnFailure_WhenTokenIsInvalid()
    {
        // Act
        var result = await _service.ValidateTokenAsync("invalid-token");

        // Assert
        result.IsSuccess.Should().BeFalse();
        // Invalid token format causes an exception that results in Unknown error code
        // TokenInvalid is for tokens that fail signature/issuer validation
        result.ErrorCode.Should().BeOneOf(AuthenticationErrorCode.TokenInvalid, AuthenticationErrorCode.Unknown);
    }

    [Fact]
    public async Task ValidateTokenAsync_ShouldReturnFailure_WhenTokenIsRevoked()
    {
        // Arrange
        var username = "testuser";
        var password = "password123";
        var authResult = AuthenticationResult.Success(
            userId: "user-123",
            username: username);

        _mockConnector.Setup(c => c.AuthenticateAsync(username, password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _mockMfaProvider.Setup(m => m.IsEnabledAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var loginResult = await _service.AuthenticateAsync(username, password);

        // Revoke the token
        await _service.RevokeTokenAsync(loginResult.Token!);

        // Act
        var result = await _service.ValidateTokenAsync(loginResult.Token!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthenticationErrorCode.TokenInvalid);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnNewTokens_WhenRefreshTokenIsValid()
    {
        // Arrange
        var username = "testuser";
        var password = "password123";
        var authResult = AuthenticationResult.Success(
            userId: "user-123",
            username: username,
            email: "test@example.com");

        _mockConnector.Setup(c => c.AuthenticateAsync(username, password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _mockConnector.Setup(c => c.GetUserInfoAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserInfo { UserId = "user-123", Username = username, Email = "test@example.com" });

        _mockConnector.Setup(c => c.GetUserGroupsAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Admins" });

        _mockMfaProvider.Setup(m => m.IsEnabledAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var loginResult = await _service.AuthenticateAsync(username, password);

        // Act
        var result = await _service.RefreshTokenAsync(loginResult.RefreshToken!);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Token.Should().NotBeNullOrEmpty();
        result.Token.Should().NotBe(loginResult.Token);
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnFailure_WhenRefreshTokenIsInvalid()
    {
        // Act
        var result = await _service.RefreshTokenAsync("invalid-refresh-token");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthenticationErrorCode.TokenInvalid);
    }

    [Fact]
    public async Task RevokeAllUserTokensAsync_ShouldInvalidateAllTokens()
    {
        // Arrange
        var username = "testuser";
        var password = "password123";
        var authResult = AuthenticationResult.Success(
            userId: "user-123",
            username: username);

        _mockConnector.Setup(c => c.AuthenticateAsync(username, password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _mockMfaProvider.Setup(m => m.IsEnabledAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Login twice to create multiple tokens
        var login1 = await _service.AuthenticateAsync(username, password);
        var login2 = await _service.AuthenticateAsync(username, password);

        // Act
        await _service.RevokeAllUserTokensAsync("user-123");

        // Assert
        (await _service.ValidateTokenAsync(login1.Token!)).IsSuccess.Should().BeFalse();
        (await _service.ValidateTokenAsync(login2.Token!)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void GetAvailableProviders_ShouldReturnConnectorNames()
    {
        // Act
        var providers = _service.GetAvailableProviders();

        // Assert
        providers.Should().Contain("Test");
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldReturnFailure_WhenProviderNotFound()
    {
        // Act
        var result = await _service.AuthenticateAsync("user", "pass", "NonExistentProvider");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthenticationErrorCode.ConfigurationError);
    }

    [Fact]
    public async Task SetupMfaAsync_ShouldDelegateToMfaProvider()
    {
        // Arrange
        var userId = "user-123";
        var displayName = "Test User";
        var expectedResult = MfaSetupResult.Success("secret", "qrcode", "manual");

        _mockMfaProvider.Setup(m => m.SetupAsync(userId, displayName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.SetupMfaAsync(userId, displayName);

        // Assert
        result.Should().BeEquivalentTo(expectedResult);
        _mockMfaProvider.Verify(m => m.SetupAsync(userId, displayName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableMfaAsync_ShouldDelegateToMfaProvider()
    {
        // Arrange
        var userId = "user-123";
        _mockMfaProvider.Setup(m => m.DisableAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DisableMfaAsync(userId);

        // Assert
        result.Should().BeTrue();
        _mockMfaProvider.Verify(m => m.DisableAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldDelegateToConnector()
    {
        // Arrange
        var userId = "user-123";
        var currentPassword = "oldpass";
        var newPassword = "newpass";

        _mockConnector.Setup(c => c.ChangePasswordAsync(userId, currentPassword, newPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ChangePasswordAsync(userId, currentPassword, newPassword);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserInfoAsync_ShouldReturnUserInfo_WhenFound()
    {
        // Arrange
        var userId = "user-123";
        var expectedUserInfo = new UserInfo
        {
            UserId = userId,
            Username = "testuser",
            Email = "test@example.com"
        };

        _mockConnector.Setup(c => c.GetUserInfoAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUserInfo);

        // Act
        var result = await _service.GetUserInfoAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task IsUserInRoleAsync_ShouldReturnTrue_WhenUserHasRole()
    {
        // Arrange
        var userId = "user-123";
        _options.GroupToRoleMappings["Admins"] = "Admin";

        var service = new IdentityService(
            Options.Create(_options),
            new[] { _mockConnector.Object },
            _mockMfaProvider.Object,
            _cache);

        _mockConnector.Setup(c => c.GetUserGroupsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Admins" });

        // Act
        var result = await service.IsUserInRoleAsync(userId, "Admin");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsUserInRoleAsync_ShouldReturnFalse_WhenUserDoesNotHaveRole()
    {
        // Arrange
        var userId = "user-123";

        _mockConnector.Setup(c => c.GetUserGroupsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Users" });

        // Act
        var result = await _service.IsUserInRoleAsync(userId, "Admin");

        // Assert
        result.Should().BeFalse();
    }
}
