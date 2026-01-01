namespace EnterpriseDataManager.Core.Events;

using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Entities.Common;

public sealed record RecoveryJobCreatedEvent(
    Guid RecoveryJobId,
    Guid ArchiveJobId,
    string DestinationPath) : DomainEvent;

public sealed record RecoveryJobStartedEvent(
    Guid RecoveryJobId,
    int TotalItems,
    long TotalBytes) : DomainEvent;

public sealed record RecoveryJobCompletedEvent(
    Guid RecoveryJobId,
    int RecoveredItems,
    long RecoveredBytes,
    TimeSpan Duration) : DomainEvent;

public sealed record RecoveryJobFailedEvent(
    Guid RecoveryJobId,
    string Reason) : DomainEvent;

public sealed record RecoveryJobProgressEvent(
    Guid RecoveryJobId,
    int ItemsRecovered,
    long BytesRecovered,
    double PercentComplete) : DomainEvent;

public sealed record RecoveryJobCanceledEvent(Guid RecoveryJobId) : DomainEvent;
