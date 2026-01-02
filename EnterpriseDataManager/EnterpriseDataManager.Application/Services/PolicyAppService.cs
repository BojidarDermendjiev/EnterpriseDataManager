namespace EnterpriseDataManager.Application.Services;

using AutoMapper;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;

public interface IPolicyAppService
{
    Task<bool> CanDeleteArchiveAsync(Guid archiveJobId, CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> GetArchiveExpiryDateAsync(Guid archiveJobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveJobDto>> GetArchivesForDeletionAsync(CancellationToken cancellationToken = default);
    Task<PolicyValidationResultDto> ValidateArchiveOperationAsync(Guid archiveJobId, string operation, CancellationToken cancellationToken = default);
}

public record PolicyValidationResultDto(
    bool IsValid,
    IReadOnlyList<PolicyViolationDto> Violations);

public record PolicyViolationDto(
    string Code,
    string Message,
    string Severity);

public sealed class PolicyAppService : IPolicyAppService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPolicyEngine _policyEngine;
    private readonly IMapper _mapper;

    public PolicyAppService(
        IUnitOfWork unitOfWork,
        IPolicyEngine policyEngine,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _policyEngine = policyEngine;
        _mapper = mapper;
    }

    public async Task<bool> CanDeleteArchiveAsync(
        Guid archiveJobId,
        CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(archiveJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(archiveJobId);

        var isExpired = await _policyEngine.IsRetentionExpiredAsync(job, cancellationToken);
        var isUnderHold = await _policyEngine.IsUnderLegalHoldAsync(job, cancellationToken);

        return isExpired && !isUnderHold;
    }

    public async Task<DateTimeOffset?> GetArchiveExpiryDateAsync(
        Guid archiveJobId,
        CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(archiveJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(archiveJobId);

        var isUnderHold = await _policyEngine.IsUnderLegalHoldAsync(job, cancellationToken);

        if (isUnderHold)
        {
            return null;
        }

        return await _policyEngine.CalculateExpiryDateAsync(job, cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveJobDto>> GetArchivesForDeletionAsync(
        CancellationToken cancellationToken = default)
    {
        var expiredArchives = await _policyEngine.GetExpiredArchivesAsync(cancellationToken);

        var eligibleForDeletion = new List<ArchiveJobDto>();

        foreach (var job in expiredArchives)
        {
            var isUnderHold = await _policyEngine.IsUnderLegalHoldAsync(job, cancellationToken);
            if (!isUnderHold)
            {
                eligibleForDeletion.Add(_mapper.Map<ArchiveJobDto>(job));
            }
        }

        return eligibleForDeletion;
    }

    public async Task<PolicyValidationResultDto> ValidateArchiveOperationAsync(
        Guid archiveJobId,
        string operation,
        CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(archiveJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(archiveJobId);

        PolicyValidationResult result = operation.ToLowerInvariant() switch
        {
            "archive" => await _policyEngine.ValidateArchiveAsync(job, cancellationToken),
            "delete" => await _policyEngine.ValidateDeletionAsync(job, cancellationToken),
            _ => throw new ArgumentException($"Unknown operation: {operation}", nameof(operation))
        };

        var violations = result.Violations
            .Select(v => new PolicyViolationDto(v.Code, v.Message, v.Severity.ToString()))
            .ToList();

        return new PolicyValidationResultDto(result.IsValid, violations);
    }
}
