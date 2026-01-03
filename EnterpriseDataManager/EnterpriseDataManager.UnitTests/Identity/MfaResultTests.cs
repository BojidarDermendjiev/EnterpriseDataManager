using EnterpriseDataManager.Infrastructure.Identity.MfaProviders;
using FluentAssertions;

namespace EnterpriseDataManager.UnitTests.Identity;

public class MfaSetupResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var qrCodeUri = "otpauth://totp/Test:user@example.com?secret=JBSWY3DPEHPK3PXP";
        var manualEntryKey = "JBSW Y3DP EHPK 3PXP";
        var backupCodes = new[] { "12345-67890", "abcde-fghij" };

        // Act
        var result = MfaSetupResult.Success(secret, qrCodeUri, manualEntryKey, backupCodes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Secret.Should().Be(secret);
        result.QrCodeUri.Should().Be(qrCodeUri);
        result.ManualEntryKey.Should().Be(manualEntryKey);
        result.BackupCodes.Should().BeEquivalentTo(backupCodes);
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Success_ShouldHandleNullBackupCodes()
    {
        // Act
        var result = MfaSetupResult.Success("secret", "qrcode", "manual");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.BackupCodes.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "MFA already enabled";
        var errorCode = MfaSetupErrorCode.AlreadyEnabled;

        // Act
        var result = MfaSetupResult.Failure(errorMessage, errorCode);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().Be(errorCode);
        result.Secret.Should().BeNull();
        result.QrCodeUri.Should().BeNull();
        result.ManualEntryKey.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldUseUnknownErrorCodeByDefault()
    {
        // Act
        var result = MfaSetupResult.Failure("Some error");

        // Assert
        result.ErrorCode.Should().Be(MfaSetupErrorCode.Unknown);
    }

    [Theory]
    [InlineData(MfaSetupErrorCode.Unknown, 0)]
    [InlineData(MfaSetupErrorCode.AlreadyEnabled, 1)]
    [InlineData(MfaSetupErrorCode.UserNotFound, 2)]
    [InlineData(MfaSetupErrorCode.InvalidConfiguration, 3)]
    [InlineData(MfaSetupErrorCode.StorageError, 4)]
    public void MfaSetupErrorCode_ShouldHaveExpectedValues(MfaSetupErrorCode code, int expectedValue)
    {
        ((int)code).Should().Be(expectedValue);
    }
}

public class MfaVerificationResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = MfaVerificationResult.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
        result.IsLockedOut.Should().BeFalse();
        result.LockoutEnd.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "Invalid code";
        var errorCode = MfaVerificationErrorCode.InvalidCode;
        var remainingAttempts = 2;

        // Act
        var result = MfaVerificationResult.Failure(errorMessage, errorCode, remainingAttempts);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().Be(errorCode);
        result.RemainingAttempts.Should().Be(remainingAttempts);
        result.IsLockedOut.Should().BeFalse();
    }

    [Fact]
    public void Failure_ShouldUseDefaultRemainingAttempts()
    {
        // Act
        var result = MfaVerificationResult.Failure("Error", MfaVerificationErrorCode.InvalidCode);

        // Assert
        result.RemainingAttempts.Should().Be(-1);
    }

    [Fact]
    public void LockedOut_ShouldCreateLockedOutResult()
    {
        // Arrange
        var lockoutEnd = DateTime.UtcNow.AddMinutes(15);

        // Act
        var result = MfaVerificationResult.LockedOut(lockoutEnd);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsLockedOut.Should().BeTrue();
        result.LockoutEnd.Should().Be(lockoutEnd);
        result.ErrorCode.Should().Be(MfaVerificationErrorCode.LockedOut);
        result.RemainingAttempts.Should().Be(0);
        result.ErrorMessage.Should().Contain("locked");
    }

    [Theory]
    [InlineData(MfaVerificationErrorCode.Unknown, 0)]
    [InlineData(MfaVerificationErrorCode.InvalidCode, 1)]
    [InlineData(MfaVerificationErrorCode.CodeExpired, 2)]
    [InlineData(MfaVerificationErrorCode.NotEnabled, 3)]
    [InlineData(MfaVerificationErrorCode.UserNotFound, 4)]
    [InlineData(MfaVerificationErrorCode.LockedOut, 5)]
    [InlineData(MfaVerificationErrorCode.BackupCodeAlreadyUsed, 6)]
    public void MfaVerificationErrorCode_ShouldHaveExpectedValues(MfaVerificationErrorCode code, int expectedValue)
    {
        ((int)code).Should().Be(expectedValue);
    }
}

public class MfaUserStateTests
{
    [Fact]
    public void MfaUserState_ShouldHaveDefaultValues()
    {
        // Act
        var state = new MfaUserState();

        // Assert
        state.UserId.Should().BeEmpty();
        state.Secret.Should().BeNull();
        state.IsEnabled.Should().BeFalse();
        state.EnabledAt.Should().BeNull();
        state.FailedAttempts.Should().Be(0);
        state.LockoutEnd.Should().BeNull();
        state.BackupCodes.Should().BeEmpty();
        state.UsedBackupCodes.Should().BeEmpty();
        state.LastVerifiedAt.Should().BeNull();
    }

    [Fact]
    public void MfaUserState_ShouldStoreAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var state = new MfaUserState
        {
            UserId = "user-123",
            Method = MfaMethod.Totp,
            Secret = "secret-key",
            IsEnabled = true,
            EnabledAt = now,
            FailedAttempts = 2,
            LockoutEnd = now.AddMinutes(15),
            BackupCodes = new List<string> { "code1", "code2" },
            UsedBackupCodes = new List<string> { "used1" },
            LastVerifiedAt = now
        };

        // Assert
        state.UserId.Should().Be("user-123");
        state.Method.Should().Be(MfaMethod.Totp);
        state.Secret.Should().Be("secret-key");
        state.IsEnabled.Should().BeTrue();
        state.EnabledAt.Should().Be(now);
        state.FailedAttempts.Should().Be(2);
        state.LockoutEnd.Should().Be(now.AddMinutes(15));
        state.BackupCodes.Should().HaveCount(2);
        state.UsedBackupCodes.Should().HaveCount(1);
        state.LastVerifiedAt.Should().Be(now);
    }

    [Theory]
    [InlineData(MfaMethod.Totp, 1)]
    [InlineData(MfaMethod.Sms, 2)]
    [InlineData(MfaMethod.Email, 3)]
    [InlineData(MfaMethod.Push, 4)]
    [InlineData(MfaMethod.Fido2, 5)]
    [InlineData(MfaMethod.BackupCode, 6)]
    public void MfaMethod_ShouldHaveExpectedValues(MfaMethod method, int expectedValue)
    {
        ((int)method).Should().Be(expectedValue);
    }
}

public class InMemoryMfaStateStoreTests
{
    private readonly InMemoryMfaStateStore _store;

    public InMemoryMfaStateStoreTests()
    {
        _store = new InMemoryMfaStateStore();
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnNull_WhenUserNotFound()
    {
        // Act
        var result = await _store.GetStateAsync("nonexistent-user");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveStateAsync_ShouldPersistState()
    {
        // Arrange
        var state = new MfaUserState
        {
            UserId = "user-123",
            Method = MfaMethod.Totp,
            Secret = "secret",
            IsEnabled = true
        };

        // Act
        await _store.SaveStateAsync(state);
        var retrieved = await _store.GetStateAsync("user-123");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.UserId.Should().Be("user-123");
        retrieved.Secret.Should().Be("secret");
        retrieved.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task SaveStateAsync_ShouldUpdateExistingState()
    {
        // Arrange
        var state = new MfaUserState
        {
            UserId = "user-123",
            Secret = "old-secret"
        };
        await _store.SaveStateAsync(state);

        // Act
        state.Secret = "new-secret";
        await _store.SaveStateAsync(state);
        var retrieved = await _store.GetStateAsync("user-123");

        // Assert
        retrieved!.Secret.Should().Be("new-secret");
    }

    [Fact]
    public async Task DeleteStateAsync_ShouldRemoveState()
    {
        // Arrange
        var state = new MfaUserState { UserId = "user-123" };
        await _store.SaveStateAsync(state);

        // Act
        var result = await _store.DeleteStateAsync("user-123");
        var retrieved = await _store.GetStateAsync("user-123");

        // Assert
        result.Should().BeTrue();
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteStateAsync_ShouldReturnFalse_WhenUserNotFound()
    {
        // Act
        var result = await _store.DeleteStateAsync("nonexistent-user");

        // Assert
        result.Should().BeFalse();
    }
}
