using EnterpriseDataManager.Infrastructure.Identity.MfaProviders;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EnterpriseDataManager.UnitTests.Identity;

public class TotpMfaProviderTests
{
    private readonly TotpMfaProvider _provider;
    private readonly InMemoryMfaStateStore _stateStore;
    private readonly TotpOptions _options;

    public TotpMfaProviderTests()
    {
        _options = new TotpOptions
        {
            Issuer = "TestApp",
            SecretLength = 20,
            CodeLength = 6,
            TimeStepSeconds = 30,
            AllowedTimeStepDrift = 1,
            MaxFailedAttempts = 3,
            LockoutDuration = TimeSpan.FromMinutes(5),
            BackupCodeCount = 10
        };

        _stateStore = new InMemoryMfaStateStore();
        _provider = new TotpMfaProvider(Options.Create(_options), _stateStore);
    }

    [Fact]
    public void ProviderName_ShouldBeTOTP()
    {
        _provider.ProviderName.Should().Be("TOTP");
    }

    [Fact]
    public void Method_ShouldBeTotp()
    {
        _provider.Method.Should().Be(MfaMethod.Totp);
    }

    [Fact]
    public async Task SetupAsync_ShouldGenerateSecretAndBackupCodes()
    {
        // Arrange
        var userId = "user-123";
        var displayName = "Test User";

        // Act
        var result = await _provider.SetupAsync(userId, displayName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Secret.Should().NotBeNullOrEmpty();
        result.QrCodeUri.Should().NotBeNullOrEmpty();
        result.QrCodeUri.Should().Contain("otpauth://totp/");
        result.QrCodeUri.Should().Contain(_options.Issuer);
        result.ManualEntryKey.Should().NotBeNullOrEmpty();
        result.BackupCodes.Should().NotBeNull();
        result.BackupCodes.Should().HaveCount(_options.BackupCodeCount);
    }

    [Fact]
    public async Task SetupAsync_ShouldFailIfAlreadyEnabled()
    {
        // Arrange
        var userId = "user-123";
        await _provider.SetupAsync(userId);

        // Enable MFA by verifying a code (simulate)
        var state = await _stateStore.GetStateAsync(userId);
        state!.IsEnabled = true;
        await _stateStore.SaveStateAsync(state);

        // Act
        var result = await _provider.SetupAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(MfaSetupErrorCode.AlreadyEnabled);
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnFalseForNewUser()
    {
        // Arrange
        var userId = "user-new";

        // Act
        var isEnabled = await _provider.IsEnabledAsync(userId);

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnTrueAfterMfaEnabled()
    {
        // Arrange
        var userId = "user-123";
        await _provider.SetupAsync(userId);

        var state = await _stateStore.GetStateAsync(userId);
        state!.IsEnabled = true;
        await _stateStore.SaveStateAsync(state);

        // Act
        var isEnabled = await _provider.IsEnabledAsync(userId);

        // Assert
        isEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_ShouldFailIfMfaNotConfigured()
    {
        // Arrange
        var userId = "user-nonexistent";

        // Act
        var result = await _provider.VerifyAsync(userId, "123456");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(MfaVerificationErrorCode.NotEnabled);
    }

    [Fact]
    public async Task VerifyAsync_ShouldFailWithInvalidCode()
    {
        // Arrange
        var userId = "user-123";
        await _provider.SetupAsync(userId);

        // Act
        var result = await _provider.VerifyAsync(userId, "000000");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(MfaVerificationErrorCode.InvalidCode);
        result.RemainingAttempts.Should().Be(_options.MaxFailedAttempts - 1);
    }

    [Fact]
    public async Task VerifyAsync_ShouldLockoutAfterMaxFailedAttempts()
    {
        // Arrange
        var userId = "user-123";
        await _provider.SetupAsync(userId);

        // Act - exhaust all attempts
        for (int i = 0; i < _options.MaxFailedAttempts; i++)
        {
            await _provider.VerifyAsync(userId, "000000");
        }

        var result = await _provider.VerifyAsync(userId, "000000");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsLockedOut.Should().BeTrue();
        result.ErrorCode.Should().Be(MfaVerificationErrorCode.LockedOut);
        result.LockoutEnd.Should().NotBeNull();
        result.LockoutEnd.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task DisableAsync_ShouldRemoveMfaState()
    {
        // Arrange
        var userId = "user-123";
        await _provider.SetupAsync(userId);

        // Act
        var result = await _provider.DisableAsync(userId);

        // Assert
        result.Should().BeTrue();
        (await _provider.IsEnabledAsync(userId)).Should().BeFalse();
    }

    [Fact]
    public async Task GenerateBackupCodesAsync_ShouldGenerateNewCodes()
    {
        // Arrange
        var userId = "user-123";
        await _provider.SetupAsync(userId);

        // Act
        var codes = await _provider.GenerateBackupCodesAsync(userId, 5);

        // Assert
        codes.Should().HaveCount(5);
        codes.All(c => c.Contains('-')).Should().BeTrue();
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_ShouldSucceedWithValidCode()
    {
        // Arrange
        var userId = "user-123";
        var setupResult = await _provider.SetupAsync(userId);

        // Enable MFA
        var state = await _stateStore.GetStateAsync(userId);
        state!.IsEnabled = true;
        await _stateStore.SaveStateAsync(state);

        var backupCode = setupResult.BackupCodes!.First();

        // Act
        var result = await _provider.VerifyBackupCodeAsync(userId, backupCode);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_ShouldFailWithUsedCode()
    {
        // Arrange
        var userId = "user-123";
        var setupResult = await _provider.SetupAsync(userId);

        // Enable MFA
        var state = await _stateStore.GetStateAsync(userId);
        state!.IsEnabled = true;
        await _stateStore.SaveStateAsync(state);

        var backupCode = setupResult.BackupCodes!.First();

        // Use the code once
        await _provider.VerifyBackupCodeAsync(userId, backupCode);

        // Act - try to use again
        var result = await _provider.VerifyBackupCodeAsync(userId, backupCode);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(MfaVerificationErrorCode.BackupCodeAlreadyUsed);
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_ShouldFailWithInvalidCode()
    {
        // Arrange
        var userId = "user-123";
        await _provider.SetupAsync(userId);

        // Enable MFA
        var state = await _stateStore.GetStateAsync(userId);
        state!.IsEnabled = true;
        await _stateStore.SaveStateAsync(state);

        // Act
        var result = await _provider.VerifyBackupCodeAsync(userId, "invalid-code");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(MfaVerificationErrorCode.InvalidCode);
    }

    [Fact]
    public async Task SetupAsync_ShouldGenerateValidQrCodeUri()
    {
        // Arrange
        var userId = "user-123";
        var displayName = "test@example.com";

        // Act
        var result = await _provider.SetupAsync(userId, displayName);

        // Assert
        result.QrCodeUri.Should().StartWith("otpauth://totp/");
        result.QrCodeUri.Should().Contain($"secret={result.Secret}");
        result.QrCodeUri.Should().Contain($"issuer={Uri.EscapeDataString(_options.Issuer)}");
        result.QrCodeUri.Should().Contain($"digits={_options.CodeLength}");
        result.QrCodeUri.Should().Contain($"period={_options.TimeStepSeconds}");
    }

    [Fact]
    public async Task ManualEntryKey_ShouldBeFormattedWithSpaces()
    {
        // Arrange
        var userId = "user-123";

        // Act
        var result = await _provider.SetupAsync(userId);

        // Assert
        result.ManualEntryKey.Should().Contain(" ");
        // Remove spaces and compare with secret
        result.ManualEntryKey!.Replace(" ", "").Should().Be(result.Secret);
    }
}
