namespace EnterpriseDataManager.Core.Exceptions;

public class PolicyViolationException : DomainException
{
    public Guid? RetentionPolicyId { get; }
    public Guid? ArchiveJobId { get; }
    public IReadOnlyList<string> Violations { get; }

    public PolicyViolationException(string code, string message, Exception? innerException = null)
        : base(code, message, innerException)
    {
        Violations = Array.Empty<string>();
    }

    public PolicyViolationException(string code, string message, IEnumerable<string> violations, Guid? policyId = null, Guid? archiveJobId = null, Exception? innerException = null)
        : base(code, message, innerException)
    {
        RetentionPolicyId = policyId;
        ArchiveJobId = archiveJobId;
        Violations = violations.ToList().AsReadOnly();
    }

    public static PolicyViolationException PolicyNotFound(Guid policyId)
        => new(DomainErrorCodes.RetentionPolicyNotFound, $"Retention policy with ID '{policyId}' was not found.");

    public static PolicyViolationException ForPolicyImmutable(Guid policyId)
        => new(DomainErrorCodes.PolicyImmutable, $"Retention policy with ID '{policyId}' is immutable and cannot be modified.", Enumerable.Empty<string>(), policyId: policyId);

    public static PolicyViolationException ForLegalHoldActive(Guid policyId)
        => new(DomainErrorCodes.LegalHoldActive, $"Retention policy with ID '{policyId}' is under legal hold. Data cannot be deleted.", Enumerable.Empty<string>(), policyId: policyId);

    public static PolicyViolationException RetentionNotExpired(Guid archiveJobId, DateTimeOffset expiryDate)
        => new(DomainErrorCodes.RetentionPeriodNotExpired, $"Archive job '{archiveJobId}' retention period has not expired. Expiry date: {expiryDate:O}", Enumerable.Empty<string>(), archiveJobId: archiveJobId);

    public static PolicyViolationException ValidationFailed(Guid archiveJobId, IEnumerable<string> violations)
        => new(DomainErrorCodes.PolicyViolation, $"Policy validation failed for archive job '{archiveJobId}'.", violations, archiveJobId: archiveJobId);

    public static PolicyViolationException DeletionBlocked(Guid archiveJobId, string reason)
        => new(DomainErrorCodes.PolicyViolation, $"Deletion of archive job '{archiveJobId}' is blocked: {reason}", new[] { reason }, archiveJobId: archiveJobId);
}

public class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message, IDictionary<string, string[]> errors)
        : base(DomainErrorCodes.ValidationFailed, message)
    {
        Errors = errors.AsReadOnly();
    }

    public ValidationException(string propertyName, string error)
        : base(DomainErrorCodes.ValidationFailed, $"Validation failed for '{propertyName}': {error}")
    {
        Errors = new Dictionary<string, string[]> { { propertyName, new[] { error } } }.AsReadOnly();
    }

    public static ValidationException ForProperty(string propertyName, string error)
        => new(propertyName, error);

    public static ValidationException ForProperties(IDictionary<string, string[]> errors)
        => new("One or more validation errors occurred.", errors);
}

public class EntityNotFoundException : DomainException
{
    public string EntityType { get; }
    public object EntityId { get; }

    public EntityNotFoundException(string entityType, object entityId)
        : base("ENTITY_NOT_FOUND", $"{entityType} with ID '{entityId}' was not found.")
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    public static EntityNotFoundException For<T>(Guid id) where T : class
        => new(typeof(T).Name, id);
}

public class ConcurrencyException : DomainException
{
    public string EntityType { get; }
    public object EntityId { get; }

    public ConcurrencyException(string entityType, object entityId)
        : base(DomainErrorCodes.ConcurrencyConflict, $"A concurrency conflict occurred while updating {entityType} with ID '{entityId}'. The entity may have been modified or deleted by another process.")
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    public static ConcurrencyException For<T>(Guid id) where T : class
        => new(typeof(T).Name, id);
}
