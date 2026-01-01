namespace EnterpriseDataManager.Core.Entities;

using EnterpriseDataManager.Common;
using EnterpriseDataManager.Core.Entities.Common;
using static EnterpriseDataManager.Common.EntityValidationConstants;
public class AuditRecord : BaseEntity
{
    public DateTimeOffset Timestamp { get; private set; }
    public string Actor { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string? ResourceType { get; private set; }
    public string? ResourceId { get; private set; }
    public bool Success { get; private set; }
    public string? Details { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? CorrelationId { get; private set; }

    protected AuditRecord() { }

    private AuditRecord(string actor, string action, bool success)
    {
        Timestamp = DateTimeOffset.UtcNow;
        Actor = actor;
        Action = action;
        Success = success;
    }

    public static AuditRecord Create(string actor, string action, bool success = true)
    {
        Guard.AgainstNullOrWhiteSpace(actor, ActorCannotBeEmpty);
        Guard.AgainstNullOrWhiteSpace(action, ActionCannotBeEmpty);

        return new AuditRecord(actor.Trim(), action.Trim(), success);
    }

    public AuditRecord WithResource(string resourceType, string resourceId)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
        return this;
    }

    public AuditRecord WithDetails(string details)
    {
        Details = details;
        return this;
    }

    public AuditRecord WithClientInfo(string? ipAddress, string? userAgent)
    {
        IpAddress = ipAddress;
        UserAgent = userAgent;
        return this;
    }

    public AuditRecord WithCorrelationId(string correlationId)
    {
        CorrelationId = correlationId;
        return this;
    }
}
