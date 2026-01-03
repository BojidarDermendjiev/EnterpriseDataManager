using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseDataManager.Infrastructure.Identity.MfaProviders;

public class TotpMfaProvider : IMfaProvider
{
    private readonly TotpOptions _options;
    private readonly ILogger<TotpMfaProvider>? _logger;
    private readonly IMfaStateStore _stateStore;

    public string ProviderName => "TOTP";
    public MfaMethod Method => MfaMethod.Totp;

    public TotpMfaProvider(
        IOptions<TotpOptions> options,
        IMfaStateStore stateStore,
        ILogger<TotpMfaProvider>? logger = null)
    {
        _options = options.Value;
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task<MfaSetupResult> SetupAsync(
        string userId,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existingState = await _stateStore.GetStateAsync(userId, cancellationToken);
            if (existingState?.IsEnabled == true)
            {
                _logger?.LogWarning("MFA already enabled for user: {UserId}", userId);
                return MfaSetupResult.Failure("MFA is already enabled", MfaSetupErrorCode.AlreadyEnabled);
            }

            // Generate a new secret
            var secret = GenerateSecret();
            var base32Secret = Base32Encode(secret);

            // Generate backup codes
            var backupCodes = GenerateBackupCodes(_options.BackupCodeCount);

            // Create the otpauth URI for QR code
            var accountName = displayName ?? userId;
            var qrCodeUri = GenerateOtpAuthUri(base32Secret, accountName);

            // Store the state (not enabled until verified)
            var state = new MfaUserState
            {
                UserId = userId,
                Method = MfaMethod.Totp,
                Secret = Convert.ToBase64String(secret),
                IsEnabled = false,
                BackupCodes = backupCodes.Select(HashBackupCode).ToList()
            };

            await _stateStore.SaveStateAsync(state, cancellationToken);

            _logger?.LogInformation("MFA setup initiated for user: {UserId}", userId);

            return MfaSetupResult.Success(
                secret: base32Secret,
                qrCodeUri: qrCodeUri,
                manualEntryKey: FormatManualEntryKey(base32Secret),
                backupCodes: backupCodes);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting up MFA for user: {UserId}", userId);
            return MfaSetupResult.Failure($"Setup failed: {ex.Message}", MfaSetupErrorCode.StorageError);
        }
    }

    public async Task<MfaVerificationResult> VerifyAsync(
        string userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _stateStore.GetStateAsync(userId, cancellationToken);
            if (state == null)
            {
                return MfaVerificationResult.Failure("MFA not configured", MfaVerificationErrorCode.NotEnabled);
            }

            // Check lockout
            if (state.LockoutEnd.HasValue && state.LockoutEnd.Value > DateTime.UtcNow)
            {
                return MfaVerificationResult.LockedOut(state.LockoutEnd.Value);
            }

            // Reset lockout if expired
            if (state.LockoutEnd.HasValue && state.LockoutEnd.Value <= DateTime.UtcNow)
            {
                state.LockoutEnd = null;
                state.FailedAttempts = 0;
            }

            var secret = Convert.FromBase64String(state.Secret!);
            var isValid = VerifyTotp(secret, code);

            if (isValid)
            {
                // Enable MFA if this is first successful verification
                if (!state.IsEnabled)
                {
                    state.IsEnabled = true;
                    state.EnabledAt = DateTime.UtcNow;
                }

                state.FailedAttempts = 0;
                state.LastVerifiedAt = DateTime.UtcNow;
                await _stateStore.SaveStateAsync(state, cancellationToken);

                _logger?.LogInformation("MFA verification successful for user: {UserId}", userId);
                return MfaVerificationResult.Success();
            }

            // Handle failed attempt
            state.FailedAttempts++;
            var remainingAttempts = _options.MaxFailedAttempts - state.FailedAttempts;

            if (state.FailedAttempts >= _options.MaxFailedAttempts)
            {
                state.LockoutEnd = DateTime.UtcNow.Add(_options.LockoutDuration);
                await _stateStore.SaveStateAsync(state, cancellationToken);

                _logger?.LogWarning("MFA lockout triggered for user: {UserId}", userId);
                return MfaVerificationResult.LockedOut(state.LockoutEnd.Value);
            }

            await _stateStore.SaveStateAsync(state, cancellationToken);

            _logger?.LogWarning("MFA verification failed for user: {UserId}, remaining attempts: {Remaining}",
                userId, remainingAttempts);

            return MfaVerificationResult.Failure(
                "Invalid code",
                MfaVerificationErrorCode.InvalidCode,
                remainingAttempts);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error verifying MFA for user: {UserId}", userId);
            return MfaVerificationResult.Failure($"Verification failed: {ex.Message}");
        }
    }

    public async Task<bool> IsEnabledAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var state = await _stateStore.GetStateAsync(userId, cancellationToken);
        return state?.IsEnabled ?? false;
    }

    public async Task<bool> DisableAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _stateStore.DeleteStateAsync(userId, cancellationToken);
            if (result)
            {
                _logger?.LogInformation("MFA disabled for user: {UserId}", userId);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disabling MFA for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GenerateBackupCodesAsync(
        string userId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        var state = await _stateStore.GetStateAsync(userId, cancellationToken);
        if (state == null)
        {
            return Enumerable.Empty<string>();
        }

        var backupCodes = GenerateBackupCodes(count);
        state.BackupCodes = backupCodes.Select(HashBackupCode).ToList();
        state.UsedBackupCodes.Clear();

        await _stateStore.SaveStateAsync(state, cancellationToken);

        _logger?.LogInformation("Generated {Count} new backup codes for user: {UserId}", count, userId);

        return backupCodes;
    }

    public async Task<MfaVerificationResult> VerifyBackupCodeAsync(
        string userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _stateStore.GetStateAsync(userId, cancellationToken);
            if (state == null || !state.IsEnabled)
            {
                return MfaVerificationResult.Failure("MFA not enabled", MfaVerificationErrorCode.NotEnabled);
            }

            var normalizedCode = NormalizeBackupCode(code);
            var hashedCode = HashBackupCode(normalizedCode);

            // Check if code was already used
            if (state.UsedBackupCodes.Contains(hashedCode))
            {
                _logger?.LogWarning("Attempt to reuse backup code for user: {UserId}", userId);
                return MfaVerificationResult.Failure(
                    "Backup code already used",
                    MfaVerificationErrorCode.BackupCodeAlreadyUsed);
            }

            // Find and validate the code
            var codeIndex = state.BackupCodes.IndexOf(hashedCode);
            if (codeIndex < 0)
            {
                _logger?.LogWarning("Invalid backup code for user: {UserId}", userId);
                return MfaVerificationResult.Failure("Invalid backup code", MfaVerificationErrorCode.InvalidCode);
            }

            // Mark code as used
            state.BackupCodes.RemoveAt(codeIndex);
            state.UsedBackupCodes.Add(hashedCode);
            state.LastVerifiedAt = DateTime.UtcNow;

            await _stateStore.SaveStateAsync(state, cancellationToken);

            var remainingCodes = state.BackupCodes.Count;
            _logger?.LogInformation(
                "Backup code verified for user: {UserId}, remaining codes: {Remaining}",
                userId, remainingCodes);

            return MfaVerificationResult.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error verifying backup code for user: {UserId}", userId);
            return MfaVerificationResult.Failure($"Verification failed: {ex.Message}");
        }
    }

    private byte[] GenerateSecret()
    {
        var secret = new byte[_options.SecretLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(secret);
        return secret;
    }

    private string GenerateOtpAuthUri(string secret, string accountName)
    {
        var issuer = Uri.EscapeDataString(_options.Issuer);
        var account = Uri.EscapeDataString(accountName);

        return $"otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits={_options.CodeLength}&period={_options.TimeStepSeconds}";
    }

    private bool VerifyTotp(byte[] secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != _options.CodeLength)
        {
            return false;
        }

        if (!long.TryParse(code, out _))
        {
            return false;
        }

        var currentTimeStep = GetCurrentTimeStep();

        // Check current and adjacent time steps for clock drift
        for (var i = -_options.AllowedTimeStepDrift; i <= _options.AllowedTimeStepDrift; i++)
        {
            var expectedCode = GenerateTotpCode(secret, currentTimeStep + i);
            if (ConstantTimeEquals(code, expectedCode))
            {
                return true;
            }
        }

        return false;
    }

    private long GetCurrentTimeStep()
    {
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return unixTime / _options.TimeStepSeconds;
    }

    private string GenerateTotpCode(byte[] secret, long timeStep)
    {
        var timeStepBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timeStepBytes);
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(timeStepBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var code = binaryCode % (int)Math.Pow(10, _options.CodeLength);
        return code.ToString().PadLeft(_options.CodeLength, '0');
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }

    private List<string> GenerateBackupCodes(int count)
    {
        var codes = new List<string>();
        using var rng = RandomNumberGenerator.Create();

        for (var i = 0; i < count; i++)
        {
            var bytes = new byte[5];
            rng.GetBytes(bytes);
            var code = Convert.ToHexString(bytes).ToLowerInvariant();
            // Format as XXXXX-XXXXX
            codes.Add($"{code[..5]}-{code[5..]}");
        }

        return codes;
    }

    private static string NormalizeBackupCode(string code)
    {
        return code.Replace("-", "").Replace(" ", "").ToLowerInvariant();
    }

    private static string HashBackupCode(string code)
    {
        var normalized = NormalizeBackupCode(code);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string FormatManualEntryKey(string base32Secret)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < base32Secret.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                sb.Append(' ');
            }
            sb.Append(base32Secret[i]);
        }
        return sb.ToString();
    }

    private static string Base32Encode(byte[] data)
    {
        const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder((data.Length * 8 + 4) / 5);

        var buffer = 0;
        var bufferSize = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bufferSize += 8;

            while (bufferSize >= 5)
            {
                bufferSize -= 5;
                result.Append(base32Chars[(buffer >> bufferSize) & 0x1F]);
            }
        }

        if (bufferSize > 0)
        {
            result.Append(base32Chars[(buffer << (5 - bufferSize)) & 0x1F]);
        }

        return result.ToString();
    }
}

public class TotpOptions
{
    public const string SectionName = "Identity:Mfa:Totp";

    public string Issuer { get; set; } = "EnterpriseDataManager";
    public int SecretLength { get; set; } = 20;
    public int CodeLength { get; set; } = 6;
    public int TimeStepSeconds { get; set; } = 30;
    public int AllowedTimeStepDrift { get; set; } = 1;
    public int MaxFailedAttempts { get; set; } = 5;
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
    public int BackupCodeCount { get; set; } = 10;
}

public interface IMfaStateStore
{
    Task<MfaUserState?> GetStateAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveStateAsync(MfaUserState state, CancellationToken cancellationToken = default);
    Task<bool> DeleteStateAsync(string userId, CancellationToken cancellationToken = default);
}

public class InMemoryMfaStateStore : IMfaStateStore
{
    private readonly ConcurrentDictionary<string, MfaUserState> _states = new();

    public Task<MfaUserState?> GetStateAsync(string userId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(userId, out var state);
        return Task.FromResult(state);
    }

    public Task SaveStateAsync(MfaUserState state, CancellationToken cancellationToken = default)
    {
        _states[state.UserId] = state;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteStateAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_states.TryRemove(userId, out _));
    }
}
