namespace EnterpriseDataManager.Core.Exceptions;

public class ArchivalException : DomainException
{
    public Guid? ArchivePlanId { get; }
    public Guid? ArchiveJobId { get; }
    public Guid? ArchiveItemId { get; }

    public ArchivalException(string code, string message, Exception? innerException = null)
        : base(code, message, innerException)
    {
    }

    public ArchivalException(string code, string message, Guid? planId = null, Guid? jobId = null, Guid? itemId = null, Exception? innerException = null)
        : base(code, message, innerException)
    {
        ArchivePlanId = planId;
        ArchiveJobId = jobId;
        ArchiveItemId = itemId;
    }

    public static ArchivalException PlanNotFound(Guid planId)
        => new(DomainErrorCodes.ArchivePlanNotFound, $"Archive plan with ID '{planId}' was not found.", planId: planId);

    public static ArchivalException JobNotFound(Guid jobId)
        => new(DomainErrorCodes.ArchiveJobNotFound, $"Archive job with ID '{jobId}' was not found.", jobId: jobId);

    public static ArchivalException ItemNotFound(Guid itemId)
        => new(DomainErrorCodes.ArchiveItemNotFound, $"Archive item with ID '{itemId}' was not found.", itemId: itemId);

    public static ArchivalException PlanInactive(Guid planId)
        => new(DomainErrorCodes.ArchivePlanInactive, $"Archive plan with ID '{planId}' is not active.", planId: planId);

    public static ArchivalException JobAlreadyRunning(Guid jobId)
        => new(DomainErrorCodes.ArchiveJobAlreadyRunning, $"Archive job with ID '{jobId}' is already running.", jobId: jobId);

    public static ArchivalException JobNotRunning(Guid jobId)
        => new(DomainErrorCodes.ArchiveJobNotRunning, $"Archive job with ID '{jobId}' is not running.", jobId: jobId);

    public static ArchivalException JobAlreadyCompleted(Guid jobId)
        => new(DomainErrorCodes.ArchiveJobCompleted, $"Archive job with ID '{jobId}' has already completed.", jobId: jobId);

    public static ArchivalException JobAlreadyFailed(Guid jobId)
        => new(DomainErrorCodes.ArchiveJobFailed, $"Archive job with ID '{jobId}' has already failed.", jobId: jobId);

    public static ArchivalException JobCanceled(Guid jobId)
        => new(DomainErrorCodes.ArchiveJobCanceled, $"Archive job with ID '{jobId}' was canceled.", jobId: jobId);

    public static ArchivalException InvalidStatus(Guid jobId, string currentStatus, string expectedStatus)
        => new(DomainErrorCodes.InvalidArchiveStatus, $"Archive job '{jobId}' is in '{currentStatus}' status, expected '{expectedStatus}'.", jobId: jobId);
}

public class RecoveryException : DomainException
{
    public Guid? RecoveryJobId { get; }
    public Guid? ArchiveJobId { get; }

    public RecoveryException(string code, string message, Exception? innerException = null)
        : base(code, message, innerException)
    {
    }

    public RecoveryException(string code, string message, Guid? recoveryJobId = null, Guid? archiveJobId = null, Exception? innerException = null)
        : base(code, message, innerException)
    {
        RecoveryJobId = recoveryJobId;
        ArchiveJobId = archiveJobId;
    }

    public static RecoveryException JobNotFound(Guid jobId)
        => new(DomainErrorCodes.RecoveryJobNotFound, $"Recovery job with ID '{jobId}' was not found.", recoveryJobId: jobId);

    public static RecoveryException JobAlreadyRunning(Guid jobId)
        => new(DomainErrorCodes.RecoveryJobAlreadyRunning, $"Recovery job with ID '{jobId}' is already running.", recoveryJobId: jobId);

    public static RecoveryException JobNotRunning(Guid jobId)
        => new(DomainErrorCodes.RecoveryJobNotRunning, $"Recovery job with ID '{jobId}' is not running.", recoveryJobId: jobId);

    public static RecoveryException ForIntegrityCheckFailed(Guid archiveJobId, string details)
        => new(DomainErrorCodes.IntegrityCheckFailed, $"Integrity check failed for archive job '{archiveJobId}': {details}", archiveJobId: archiveJobId);

    public static RecoveryException ForArchiveCorrupted(Guid archiveJobId, string details)
        => new(DomainErrorCodes.ArchiveCorrupted, $"Archive '{archiveJobId}' is corrupted: {details}", archiveJobId: archiveJobId);
}
