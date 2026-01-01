namespace EnterpriseDataManager.Core.Entities.Common;

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }
    string? CreatedBy { get; }
    DateTimeOffset? UpdatedAt { get; }
    string? UpdatedBy { get; }

    void SetCreated(string? createdBy = null);
    void SetUpdated(string? updatedBy = null);
}
