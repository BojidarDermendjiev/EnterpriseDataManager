using EnterpriseDataManager.Core.Entities.Common;

namespace EnterpriseDataManager.Core.Events;

public sealed record AuditRecordCreatedEvent(
    Guid AuditRecordId,
    string Actor,
    string Action,
    bool Success) : DomainEvent;

public sealed record SecurityIncidentDetectedEvent(
    Guid AuditRecordId,
    string Actor,
    string Action,
    string Details,
    string? IpAddress) : DomainEvent;

public sealed record UnauthorizedAccessAttemptEvent(
    string Actor,
    string Resource,
    string AttemptedAction,
    string? IpAddress) : DomainEvent;

public sealed record DataAccessAuditedEvent(
    Guid ResourceId,
    string ResourceType,
    string Actor,
    string AccessType) : DomainEvent;
