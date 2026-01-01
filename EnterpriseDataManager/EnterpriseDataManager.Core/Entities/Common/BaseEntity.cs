namespace EnterpriseDataManager.Core.Entities.Common;

public abstract class BaseEntity : IAuditable, ISoftDeletable
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; protected init; } = Guid.NewGuid();

    // Audit fields
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    // Soft delete fields
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void SetCreated(string? createdBy = null)
    {
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = createdBy;
    }

    public void SetUpdated(string? updatedBy = null)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Delete(string? deletedBy = null)
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedBy = deletedBy;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not BaseEntity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(BaseEntity? left, BaseEntity? right)
    {
        if (left is null && right is null)
            return true;
        if (left is null || right is null)
            return false;
        return left.Equals(right);
    }

    public static bool operator !=(BaseEntity? left, BaseEntity? right) => !(left == right);
}
