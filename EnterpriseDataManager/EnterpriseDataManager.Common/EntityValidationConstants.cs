namespace EnterpriseDataManager.Common;

public static class EntityValidationConstants
{
    // Common
    public const string IdCannotBeEmpty = "ID cannot be empty.";
    public const string NameCannotBeEmpty = "Name cannot be empty.";
    public const string PathCannotBeEmpty = "Path cannot be empty.";
    public const string ValueCannotBeNull = "Value cannot be null.";

    // Size and Range
    public const string SizeCannotBeNegative = "Size cannot be negative.";
    public const string QuotaCannotBeNegative = "Quota cannot be negative.";
    public const string RetentionPeriodMustBePositive = "Retention period must be positive.";
    public const string KeySizeMustBeInRange = "Key size must be between 128 and 4096 bits.";

    // Path
    public const string SourcePathCannotBeEmpty = "Source path cannot be empty.";
    public const string TargetPathCannotBeEmpty = "Target path cannot be empty.";
    public const string DestinationPathCannotBeEmpty = "Destination path cannot be empty.";
    public const string RelativePathCannotBeEmpty = "Relative path cannot be empty.";
    public const string PathContainsInvalidCharacters = "Path contains invalid characters.";

    // Archive
    public const string ArchiveJobIdCannotBeEmpty = "Archive job ID cannot be empty.";
    public const string CannotStartJobInCurrentStatus = "Cannot start job in {0} status.";
    public const string CannotCompleteJobInCurrentStatus = "Cannot complete job in {0} status.";
    public const string CannotFailJobInCurrentStatus = "Cannot fail job in {0} status.";
    public const string CannotCancelJobInCurrentStatus = "Cannot cancel job in {0} status.";
    public const string CannotModifyAfterJobStarted = "Cannot modify after job has started.";
    public const string CannotCreateJobForInactivePlan = "Cannot create job for inactive plan.";
    public const string CannotActivatePlanWithoutStorageProvider = "Cannot activate plan without a storage provider.";
    public const string ExpectedStatusButWas = "Expected status {0} but was {1}.";

    // Audit
    public const string ActorCannotBeEmpty = "Actor cannot be empty.";
    public const string ActionCannotBeEmpty = "Action cannot be empty.";
    public const string FailureReasonCannotBeEmpty = "Failure reason cannot be empty.";
    public const string ErrorMessageCannotBeEmpty = "Error message cannot be empty.";

    // Encryption
    public const string KeyIdCannotBeEmpty = "Key ID cannot be empty.";
    public const string AlgorithmCannotBeEmpty = "Algorithm cannot be empty.";

    // Cron
    public const string CronExpressionCannotBeEmpty = "Cron expression cannot be empty.";
    public const string InvalidCronExpression = "Invalid cron expression: {0}";

    // Storage
    public const string LocalStorageDoesNotUseEndpoints = "Local storage does not use endpoints.";
    public const string LocalStorageDoesNotUseBuckets = "Local storage does not use buckets/containers.";
    public const string CannotModifyImmutablePolicy = "Cannot modify retention period on immutable policy.";
}
