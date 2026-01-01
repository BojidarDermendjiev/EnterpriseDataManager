namespace EnterpriseDataManager.Core.Entities.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
