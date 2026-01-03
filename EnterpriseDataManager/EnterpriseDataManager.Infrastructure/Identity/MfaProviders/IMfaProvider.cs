namespace EnterpriseDataManager.Infrastructure.Identity.MfaProviders;

public interface IMfaProvider
{
    string ProviderName { get; }
    MfaMethod Method { get; }

    Task<MfaSetupResult> SetupAsync(
        string userId,
        string? displayName = null,
        CancellationToken cancellationToken = default);

    Task<MfaVerificationResult> VerifyAsync(
        string userId,
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> IsEnabledAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> DisableAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GenerateBackupCodesAsync(
        string userId,
        int count = 10,
        CancellationToken cancellationToken = default);

    Task<MfaVerificationResult> VerifyBackupCodeAsync(
        string userId,
        string code,
        CancellationToken cancellationToken = default);
}

public enum MfaMethod
{
    Totp = 1,
    Sms = 2,
    Email = 3,
    Push = 4,
    Fido2 = 5,
    BackupCode = 6
}

public class MfaSetupResult
{
    public bool IsSuccess { get; init; }
    public string? Secret { get; init; }
    public string? QrCodeUri { get; init; }
    public string? ManualEntryKey { get; init; }
    public IEnumerable<string>? BackupCodes { get; init; }
    public string? ErrorMessage { get; init; }
    public MfaSetupErrorCode? ErrorCode { get; init; }

    public static MfaSetupResult Success(
        string secret,
        string qrCodeUri,
        string manualEntryKey,
        IEnumerable<string>? backupCodes = null)
    {
        return new MfaSetupResult
        {
            IsSuccess = true,
            Secret = secret,
            QrCodeUri = qrCodeUri,
            ManualEntryKey = manualEntryKey,
            BackupCodes = backupCodes
        };
    }

    public static MfaSetupResult Failure(
        string errorMessage,
        MfaSetupErrorCode errorCode = MfaSetupErrorCode.Unknown)
    {
        return new MfaSetupResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
    }
}

public enum MfaSetupErrorCode
{
    Unknown = 0,
    AlreadyEnabled = 1,
    UserNotFound = 2,
    InvalidConfiguration = 3,
    StorageError = 4
}

public class MfaVerificationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public MfaVerificationErrorCode? ErrorCode { get; init; }
    public int RemainingAttempts { get; init; }
    public bool IsLockedOut { get; init; }
    public DateTime? LockoutEnd { get; init; }

    public static MfaVerificationResult Success()
    {
        return new MfaVerificationResult { IsSuccess = true };
    }

    public static MfaVerificationResult Failure(
        string errorMessage,
        MfaVerificationErrorCode errorCode = MfaVerificationErrorCode.Unknown,
        int remainingAttempts = -1)
    {
        return new MfaVerificationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            RemainingAttempts = remainingAttempts
        };
    }

    public static MfaVerificationResult LockedOut(DateTime lockoutEnd)
    {
        return new MfaVerificationResult
        {
            IsSuccess = false,
            ErrorMessage = "Too many failed attempts. Account temporarily locked.",
            ErrorCode = MfaVerificationErrorCode.LockedOut,
            IsLockedOut = true,
            LockoutEnd = lockoutEnd,
            RemainingAttempts = 0
        };
    }
}

public enum MfaVerificationErrorCode
{
    Unknown = 0,
    InvalidCode = 1,
    CodeExpired = 2,
    NotEnabled = 3,
    UserNotFound = 4,
    LockedOut = 5,
    BackupCodeAlreadyUsed = 6
}

public class MfaUserState
{
    public string UserId { get; set; } = string.Empty;
    public MfaMethod Method { get; set; }
    public string? Secret { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? EnabledAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public List<string> BackupCodes { get; set; } = new();
    public List<string> UsedBackupCodes { get; set; } = new();
    public DateTime? LastVerifiedAt { get; set; }
}
