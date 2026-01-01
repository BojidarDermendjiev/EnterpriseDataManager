namespace EnterpriseDataManager.Core.Interfaces.Services;

using EnterpriseDataManager.Core.Entities;
public interface IPolicyEngine
{
    Task<PolicyValidationResult> ValidateArchiveAsync(ArchiveJob job, CancellationToken cancellationToken = default);
    Task<PolicyValidationResult> ValidateRecoveryAsync(RecoveryJob job, CancellationToken cancellationToken = default);
    Task<PolicyValidationResult> ValidateDeletionAsync(ArchiveJob job, CancellationToken cancellationToken = default);
    Task<bool> IsRetentionExpiredAsync(ArchiveJob job, CancellationToken cancellationToken = default);
    Task<bool> IsUnderLegalHoldAsync(ArchiveJob job, CancellationToken cancellationToken = default);
    Task<DateTimeOffset> CalculateExpiryDateAsync(ArchiveJob job, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveJob>> GetExpiredArchivesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveJob>> GetArchivesApproachingExpiryAsync(TimeSpan warningPeriod, CancellationToken cancellationToken = default);
    Task ApplyLegalHoldAsync(Guid retentionPolicyId, string reason, CancellationToken cancellationToken = default);
    Task ReleaseLegalHoldAsync(Guid retentionPolicyId, string reason, CancellationToken cancellationToken = default);
    Task ProcessRetentionPoliciesAsync(CancellationToken cancellationToken = default);
}

public record PolicyValidationResult(
    bool IsValid,
    IReadOnlyList<PolicyViolation> Violations);

public record PolicyViolation(
    string Code,
    string Message,
    PolicyViolationSeverity Severity);

public enum PolicyViolationSeverity
{
    Warning,
    Error,
    Critical
}

public interface IRetentionPolicyService
{
    Task<RetentionPolicy> CreatePolicyAsync(string name, TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
    Task<RetentionPolicy> UpdatePolicyAsync(Guid policyId, string name, string? description, CancellationToken cancellationToken = default);
    Task<RetentionPolicy> SetRetentionPeriodAsync(Guid policyId, TimeSpan period, CancellationToken cancellationToken = default);
    Task<RetentionPolicy> EnableLegalHoldAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<RetentionPolicy> DisableLegalHoldAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<RetentionPolicy> MakeImmutableAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task DeletePolicyAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetentionPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken = default);
}
