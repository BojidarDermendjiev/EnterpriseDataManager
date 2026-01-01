namespace EnterpriseDataManager.Core.Exceptions;

public abstract class DomainException : Exception
{
    public string Code { get; }
    public IReadOnlyDictionary<string, object>? Details { get; }

    protected DomainException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    protected DomainException(string code, string message, IDictionary<string, object> details, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Details = details.AsReadOnly();
    }
}

public static class DomainErrorCodes
{
    // Archive errors
    public const string ArchivePlanNotFound = "ARCHIVE_PLAN_NOT_FOUND";
    public const string ArchiveJobNotFound = "ARCHIVE_JOB_NOT_FOUND";
    public const string ArchiveItemNotFound = "ARCHIVE_ITEM_NOT_FOUND";
    public const string ArchivePlanInactive = "ARCHIVE_PLAN_INACTIVE";
    public const string ArchiveJobAlreadyRunning = "ARCHIVE_JOB_ALREADY_RUNNING";
    public const string ArchiveJobNotRunning = "ARCHIVE_JOB_NOT_RUNNING";
    public const string ArchiveJobCompleted = "ARCHIVE_JOB_COMPLETED";
    public const string ArchiveJobFailed = "ARCHIVE_JOB_FAILED";
    public const string ArchiveJobCanceled = "ARCHIVE_JOB_CANCELED";
    public const string InvalidArchiveStatus = "INVALID_ARCHIVE_STATUS";

    // Recovery errors
    public const string RecoveryJobNotFound = "RECOVERY_JOB_NOT_FOUND";
    public const string RecoveryJobAlreadyRunning = "RECOVERY_JOB_ALREADY_RUNNING";
    public const string RecoveryJobNotRunning = "RECOVERY_JOB_NOT_RUNNING";
    public const string IntegrityCheckFailed = "INTEGRITY_CHECK_FAILED";
    public const string ArchiveCorrupted = "ARCHIVE_CORRUPTED";

    // Storage errors
    public const string StorageProviderNotFound = "STORAGE_PROVIDER_NOT_FOUND";
    public const string StorageProviderDisabled = "STORAGE_PROVIDER_DISABLED";
    public const string StorageConnectionFailed = "STORAGE_CONNECTION_FAILED";
    public const string StorageQuotaExceeded = "STORAGE_QUOTA_EXCEEDED";
    public const string StorageAccessDenied = "STORAGE_ACCESS_DENIED";
    public const string FileNotFound = "FILE_NOT_FOUND";
    public const string FileAlreadyExists = "FILE_ALREADY_EXISTS";
    public const string InvalidStoragePath = "INVALID_STORAGE_PATH";

    // Policy errors
    public const string RetentionPolicyNotFound = "RETENTION_POLICY_NOT_FOUND";
    public const string PolicyImmutable = "POLICY_IMMUTABLE";
    public const string LegalHoldActive = "LEGAL_HOLD_ACTIVE";
    public const string RetentionPeriodNotExpired = "RETENTION_PERIOD_NOT_EXPIRED";
    public const string PolicyViolation = "POLICY_VIOLATION";

    // Validation errors
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string InvalidConfiguration = "INVALID_CONFIGURATION";
    public const string DuplicateEntity = "DUPLICATE_ENTITY";
    public const string EntityInUse = "ENTITY_IN_USE";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
}
