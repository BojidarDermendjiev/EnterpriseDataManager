using System.Security.Claims;
using EnterpriseDataManager.Infrastructure.Identity.IdamConnectors;
using FluentAssertions;

namespace EnterpriseDataManager.UnitTests.Identity;

public class AuthenticationResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Arrange
        var userId = "user-123";
        var username = "testuser";
        var email = "test@example.com";
        var displayName = "Test User";
        var token = "access-token";
        var refreshToken = "refresh-token";
        var expiration = DateTime.UtcNow.AddHours(1);
        var roles = new[] { "Admin", "User" };
        var groups = new[] { "Admins", "Users" };
        var claims = new[] { new Claim("custom", "value") };

        // Act
        var result = AuthenticationResult.Success(
            userId: userId,
            username: username,
            displayName: displayName,
            email: email,
            token: token,
            refreshToken: refreshToken,
            tokenExpiration: expiration,
            roles: roles,
            groups: groups,
            claims: claims);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.UserId.Should().Be(userId);
        result.Username.Should().Be(username);
        result.DisplayName.Should().Be(displayName);
        result.Email.Should().Be(email);
        result.Token.Should().Be(token);
        result.RefreshToken.Should().Be(refreshToken);
        result.TokenExpiration.Should().Be(expiration);
        result.Roles.Should().BeEquivalentTo(roles);
        result.Groups.Should().BeEquivalentTo(groups);
        result.Claims.Should().HaveCount(1);
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
        result.RequiresMfa.Should().BeFalse();
    }

    [Fact]
    public void Success_ShouldHandleNullOptionalParameters()
    {
        // Act
        var result = AuthenticationResult.Success(
            userId: "user-123",
            username: "testuser");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Roles.Should().BeEmpty();
        result.Groups.Should().BeEmpty();
        result.Claims.Should().BeEmpty();
        result.Token.Should().BeNull();
        result.RefreshToken.Should().BeNull();
        result.TokenExpiration.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "Invalid credentials";
        var errorCode = AuthenticationErrorCode.InvalidCredentials;

        // Act
        var result = AuthenticationResult.Failure(errorMessage, errorCode);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().Be(errorCode);
        result.UserId.Should().BeNull();
        result.Username.Should().BeNull();
        result.Token.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldUseUnknownErrorCodeByDefault()
    {
        // Act
        var result = AuthenticationResult.Failure("Some error");

        // Assert
        result.ErrorCode.Should().Be(AuthenticationErrorCode.Unknown);
    }

    [Fact]
    public void MfaRequired_ShouldCreateMfaRequiredResult()
    {
        // Arrange
        var mfaSessionToken = "mfa-session-123";

        // Act
        var result = AuthenticationResult.MfaRequired(mfaSessionToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.RequiresMfa.Should().BeTrue();
        result.MfaSessionToken.Should().Be(mfaSessionToken);
        result.ErrorCode.Should().Be(AuthenticationErrorCode.MfaRequired);
    }

    [Theory]
    [InlineData(AuthenticationErrorCode.Unknown, 0)]
    [InlineData(AuthenticationErrorCode.InvalidCredentials, 1)]
    [InlineData(AuthenticationErrorCode.UserNotFound, 2)]
    [InlineData(AuthenticationErrorCode.UserLocked, 3)]
    [InlineData(AuthenticationErrorCode.UserDisabled, 4)]
    [InlineData(AuthenticationErrorCode.PasswordExpired, 5)]
    [InlineData(AuthenticationErrorCode.MfaRequired, 6)]
    [InlineData(AuthenticationErrorCode.MfaFailed, 7)]
    [InlineData(AuthenticationErrorCode.TokenExpired, 8)]
    [InlineData(AuthenticationErrorCode.TokenInvalid, 9)]
    [InlineData(AuthenticationErrorCode.ConnectionFailed, 10)]
    [InlineData(AuthenticationErrorCode.ConfigurationError, 11)]
    public void AuthenticationErrorCode_ShouldHaveExpectedValues(AuthenticationErrorCode code, int expectedValue)
    {
        ((int)code).Should().Be(expectedValue);
    }
}

public class UserInfoTests
{
    [Fact]
    public void UserInfo_ShouldHaveDefaultValues()
    {
        // Act
        var userInfo = new UserInfo();

        // Assert
        userInfo.UserId.Should().BeEmpty();
        userInfo.Username.Should().BeEmpty();
        userInfo.IsEnabled.Should().BeTrue();
        userInfo.IsLocked.Should().BeFalse();
        userInfo.Groups.Should().BeEmpty();
        userInfo.Roles.Should().BeEmpty();
        userInfo.AdditionalAttributes.Should().BeEmpty();
    }

    [Fact]
    public void UserInfo_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var userInfo = new UserInfo
        {
            UserId = "user-123",
            Username = "testuser",
            Email = "test@example.com",
            DisplayName = "Test User",
            FirstName = "Test",
            LastName = "User",
            Department = "Engineering",
            Title = "Developer",
            PhoneNumber = "+1234567890",
            IsEnabled = true,
            IsLocked = false,
            LastLoginDate = new DateTime(2024, 1, 1),
            PasswordLastSet = new DateTime(2023, 12, 1),
            AccountExpirationDate = new DateTime(2025, 1, 1),
            Groups = new[] { "Admins", "Developers" },
            Roles = new[] { "Admin" },
            AdditionalAttributes = new Dictionary<string, object> { ["custom"] = "value" }
        };

        // Assert
        userInfo.UserId.Should().Be("user-123");
        userInfo.Username.Should().Be("testuser");
        userInfo.Email.Should().Be("test@example.com");
        userInfo.DisplayName.Should().Be("Test User");
        userInfo.FirstName.Should().Be("Test");
        userInfo.LastName.Should().Be("User");
        userInfo.Department.Should().Be("Engineering");
        userInfo.Title.Should().Be("Developer");
        userInfo.PhoneNumber.Should().Be("+1234567890");
        userInfo.IsEnabled.Should().BeTrue();
        userInfo.IsLocked.Should().BeFalse();
        userInfo.LastLoginDate.Should().Be(new DateTime(2024, 1, 1));
        userInfo.PasswordLastSet.Should().Be(new DateTime(2023, 12, 1));
        userInfo.AccountExpirationDate.Should().Be(new DateTime(2025, 1, 1));
        userInfo.Groups.Should().BeEquivalentTo(new[] { "Admins", "Developers" });
        userInfo.Roles.Should().BeEquivalentTo(new[] { "Admin" });
        userInfo.AdditionalAttributes.Should().ContainKey("custom");
    }
}
