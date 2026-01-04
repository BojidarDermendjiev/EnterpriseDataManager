namespace EnterpriseDataManager.Infrastructure.Services;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

public sealed class PolicyEngine : IPolicyEngine
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PolicyEngine>? _logger;

    public PolicyEngine(
        IUnitOfWork unitOfWork,
        ILogger<PolicyEngine>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PolicyValidationResult> ValidateArchiveAsync(ArchiveJob job, CancellationToken cancellationToken = default)
    {
        var violations = new List<PolicyViolation>();

        if (job.ArchivePlan == null)
        {
            var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(job.ArchivePlanId, cancellationToken);
            if (plan == null)
            {
                violations.Add(new PolicyViolation("PLAN_NOT_FOUND", "Archive plan not found", PolicyViolationSeverity.Critical));
            }
        }

        return new PolicyValidationResult(violations.Count == 0, violations);
    }

    public async Task<PolicyValidationResult> ValidateRecoveryAsync(RecoveryJob job, CancellationToken cancellationToken = default)
    {
        var violations = new List<PolicyViolation>();

        var archiveJob = await _unitOfWork.ArchiveJobs.GetByIdAsync(job.ArchiveJobId, cancellationToken);
        if (archiveJob == null)
        {
            violations.Add(new PolicyViolation("ARCHIVE_NOT_FOUND", "Source archive job not found", PolicyViolationSeverity.Critical));
            return new PolicyValidationResult(false, violations);
        }

        if (await IsUnderLegalHoldAsync(archiveJob, cancellationToken))
        {
            violations.Add(new PolicyViolation("LEGAL_HOLD", "Archive is under legal hold - recovery requires approval", PolicyViolationSeverity.Warning));
        }

        return new PolicyValidationResult(violations.All(v => v.Severity != PolicyViolationSeverity.Critical), violations);
    }

    public async Task<PolicyValidationResult> ValidateDeletionAsync(ArchiveJob job, CancellationToken cancellationToken = default)
    {
        var violations = new List<PolicyViolation>();

        if (await IsUnderLegalHoldAsync(job, cancellationToken))
        {
            violations.Add(new PolicyViolation("LEGAL_HOLD", "Cannot delete archive under legal hold", PolicyViolationSeverity.Critical));
        }

        if (!await IsRetentionExpiredAsync(job, cancellationToken))
        {
            violations.Add(new PolicyViolation("RETENTION_ACTIVE", "Retention period has not expired", PolicyViolationSeverity.Error));
        }

        return new PolicyValidationResult(violations.Count == 0, violations);
    }

    public async Task<bool> IsRetentionExpiredAsync(ArchiveJob job, CancellationToken cancellationToken = default)
    {
        var plan = job.ArchivePlan ?? await _unitOfWork.ArchivePlans.GetByIdAsync(job.ArchivePlanId, cancellationToken);
        if (plan?.RetentionPolicyId == null)
        {
            return true; // No retention policy means no retention
        }

        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(plan.RetentionPolicyId.Value, cancellationToken);
        if (policy == null || job.CompletedAt == null)
        {
            return true;
        }

        return policy.IsExpired(job.CompletedAt.Value);
    }

    public async Task<bool> IsUnderLegalHoldAsync(ArchiveJob job, CancellationToken cancellationToken = default)
    {
        var plan = job.ArchivePlan ?? await _unitOfWork.ArchivePlans.GetByIdAsync(job.ArchivePlanId, cancellationToken);
        if (plan?.RetentionPolicyId == null)
        {
            return false;
        }

        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(plan.RetentionPolicyId.Value, cancellationToken);
        return policy?.IsLegalHold ?? false;
    }

    public async Task<DateTimeOffset> CalculateExpiryDateAsync(ArchiveJob job, CancellationToken cancellationToken = default)
    {
        var plan = job.ArchivePlan ?? await _unitOfWork.ArchivePlans.GetByIdAsync(job.ArchivePlanId, cancellationToken);
        if (plan?.RetentionPolicyId == null || job.CompletedAt == null)
        {
            return DateTimeOffset.MaxValue;
        }

        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(plan.RetentionPolicyId.Value, cancellationToken);
        if (policy == null)
        {
            return DateTimeOffset.MaxValue;
        }

        return policy.CalculateExpiryDate(job.CompletedAt.Value);
    }

    public async Task<IReadOnlyList<ArchiveJob>> GetExpiredArchivesAsync(CancellationToken cancellationToken = default)
    {
        var completedJobs = await _unitOfWork.ArchiveJobs.GetByStatusAsync(ArchiveStatus.Completed, cancellationToken);
        var expiredJobs = new List<ArchiveJob>();

        foreach (var job in completedJobs)
        {
            if (await IsRetentionExpiredAsync(job, cancellationToken) && !await IsUnderLegalHoldAsync(job, cancellationToken))
            {
                expiredJobs.Add(job);
            }
        }

        return expiredJobs;
    }

    public async Task<IReadOnlyList<ArchiveJob>> GetArchivesApproachingExpiryAsync(TimeSpan warningPeriod, CancellationToken cancellationToken = default)
    {
        var completedJobs = await _unitOfWork.ArchiveJobs.GetByStatusAsync(ArchiveStatus.Completed, cancellationToken);
        var approachingExpiry = new List<ArchiveJob>();
        var warningDate = DateTimeOffset.UtcNow.Add(warningPeriod);

        foreach (var job in completedJobs)
        {
            var expiryDate = await CalculateExpiryDateAsync(job, cancellationToken);
            if (expiryDate <= warningDate && expiryDate > DateTimeOffset.UtcNow)
            {
                approachingExpiry.Add(job);
            }
        }

        return approachingExpiry;
    }

    public async Task ApplyLegalHoldAsync(Guid retentionPolicyId, string reason, CancellationToken cancellationToken = default)
    {
        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(retentionPolicyId, cancellationToken)
            ?? throw EntityNotFoundException.ForRetentionPolicy(retentionPolicyId);

        policy.EnableLegalHold();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger?.LogInformation("Applied legal hold to retention policy {PolicyId}: {Reason}", retentionPolicyId, reason);
    }

    public async Task ReleaseLegalHoldAsync(Guid retentionPolicyId, string reason, CancellationToken cancellationToken = default)
    {
        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(retentionPolicyId, cancellationToken)
            ?? throw EntityNotFoundException.ForRetentionPolicy(retentionPolicyId);

        policy.DisableLegalHold();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger?.LogInformation("Released legal hold from retention policy {PolicyId}: {Reason}", retentionPolicyId, reason);
    }

    public async Task ProcessRetentionPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var expiredArchives = await GetExpiredArchivesAsync(cancellationToken);
        _logger?.LogInformation("Processing retention policies: found {Count} expired archives", expiredArchives.Count);

        foreach (var archive in expiredArchives)
        {
            var validation = await ValidateDeletionAsync(archive, cancellationToken);
            if (validation.IsValid)
            {
                _logger?.LogInformation("Archive {ArchiveId} is eligible for deletion", archive.Id);
            }
        }
    }
}
