namespace EnterpriseDataManager.Infrastructure.Services;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.ValueObjects;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for managing archive plans.
/// </summary>
public sealed class ArchivePlanService : IArchivePlanService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ArchivePlanService>? _logger;

    public ArchivePlanService(
        IUnitOfWork unitOfWork,
        ILogger<ArchivePlanService>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ArchivePlan> CreatePlanAsync(
        string name,
        string sourcePath,
        SecurityLevel securityLevel = SecurityLevel.Internal,
        CancellationToken cancellationToken = default)
    {
        var plan = ArchivePlan.Create(name, sourcePath, securityLevel);
        await _unitOfWork.ArchivePlans.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Created archive plan {PlanId} with name '{Name}'", plan.Id, name);
        return plan;
    }

    public async Task<ArchivePlan> UpdatePlanAsync(
        Guid planId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(planId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(planId);

        plan.UpdateDetails(name, description);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Updated archive plan {PlanId}", planId);
        return plan;
    }

    public async Task<ArchivePlan> ActivatePlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(planId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(planId);

        plan.Activate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Activated archive plan {PlanId}", planId);
        return plan;
    }

    public async Task<ArchivePlan> DeactivatePlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(planId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(planId);

        plan.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Deactivated archive plan {PlanId}", planId);
        return plan;
    }

    public async Task<ArchivePlan> SetScheduleAsync(
        Guid planId,
        string cronExpression,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(planId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(planId);

        var schedule = CronSchedule.Create(cronExpression, description);
        plan.SetSchedule(schedule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Set schedule for archive plan {PlanId}: {Cron}", planId, cronExpression);
        return plan;
    }

    public async Task<ArchivePlan> SetStorageProviderAsync(
        Guid planId,
        Guid storageProviderId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(planId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(planId);

        var provider = await _unitOfWork.StorageProviders.GetByIdAsync(storageProviderId, cancellationToken)
            ?? throw EntityNotFoundException.ForStorageProvider(storageProviderId);

        plan.SetStorageProvider(provider);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Set storage provider {ProviderId} for archive plan {PlanId}", storageProviderId, planId);
        return plan;
    }

    public async Task<ArchivePlan> SetRetentionPolicyAsync(
        Guid planId,
        Guid retentionPolicyId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(planId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(planId);

        var policy = await _unitOfWork.RetentionPolicies.GetByIdAsync(retentionPolicyId, cancellationToken)
            ?? throw EntityNotFoundException.ForRetentionPolicy(retentionPolicyId);

        plan.SetRetentionPolicy(policy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Set retention policy {PolicyId} for archive plan {PlanId}", retentionPolicyId, planId);
        return plan;
    }

    public async Task DeletePlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(planId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(planId);

        _unitOfWork.ArchivePlans.Remove(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Deleted archive plan {PlanId}", planId);
    }

    public async Task<IReadOnlyList<ArchivePlan>> GetActivePlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _unitOfWork.ArchivePlans.GetAllAsync(cancellationToken);
        return plans.Where(p => p.IsActive).ToList();
    }
}
