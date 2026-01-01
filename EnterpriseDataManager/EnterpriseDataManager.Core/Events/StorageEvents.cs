namespace EnterpriseDataManager.Core.Events;

using EnterpriseDataManager.Core.Entities.Common;
using EnterpriseDataManager.Core.Enums;
public sealed record StorageProviderCreatedEvent(
    Guid StorageProviderId,
    string Name,
    StorageType Type) : DomainEvent;

public sealed record StorageProviderUpdatedEvent(
    Guid StorageProviderId,
    string Name) : DomainEvent;

public sealed record StorageProviderEnabledEvent(Guid StorageProviderId) : DomainEvent;

public sealed record StorageProviderDisabledEvent(Guid StorageProviderId) : DomainEvent;

public sealed record StorageQuotaExceededEvent(
    Guid StorageProviderId,
    long CurrentUsage,
    long QuotaLimit) : DomainEvent;

public sealed record StorageQuotaWarningEvent(
    Guid StorageProviderId,
    long CurrentUsage,
    long QuotaLimit,
    int PercentUsed) : DomainEvent;

public sealed record StorageConnectionFailedEvent(
    Guid StorageProviderId,
    string ErrorMessage) : DomainEvent;

public sealed record StorageConnectionRestoredEvent(Guid StorageProviderId) : DomainEvent;
