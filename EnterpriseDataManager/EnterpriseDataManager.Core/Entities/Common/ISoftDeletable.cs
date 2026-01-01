namespace EnterpriseDataManager.Core.Entities.Common;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
    string? DeletedBy { get; }

    void Delete(string? deletedBy = null);
    void Restore();
}
