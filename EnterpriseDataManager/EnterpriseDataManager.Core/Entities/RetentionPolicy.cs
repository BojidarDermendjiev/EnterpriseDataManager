namespace EnterpriseDataManager.Core.Entities;

using EnterpriseDataManager.Common;
using EnterpriseDataManager.Core.Entities.Common;
using static EnterpriseDataManager.Common.EntityValidationConstants;

public class RetentionPolicy : BaseEntity
{
    private readonly List<ArchivePlan> _archivePlans = new();

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public TimeSpan RetentionPeriod { get; private set; }
    public bool IsLegalHold { get; private set; }
    public bool IsImmutable { get; private set; }
    public string? Scope { get; private set; }
    public IReadOnlyCollection<ArchivePlan> ArchivePlans => _archivePlans.AsReadOnly();

    protected RetentionPolicy() { }

    private RetentionPolicy(string name, TimeSpan retentionPeriod)
    {
        Name = name;
        RetentionPeriod = retentionPeriod;
    }

    public static RetentionPolicy Create(string name, TimeSpan retentionPeriod)
    {
        Guard.AgainstNullOrWhiteSpace(name, NameCannotBeEmpty);
        Guard.AgainstNegativeOrZero(retentionPeriod, RetentionPeriodMustBePositive);

        return new RetentionPolicy(name.Trim(), retentionPeriod);
    }

    public static RetentionPolicy CreateWithDays(string name, int days)
        => Create(name, TimeSpan.FromDays(days));

    public static RetentionPolicy CreateWithYears(string name, int years)
        => Create(name, TimeSpan.FromDays(years * 365));

    public void UpdateDetails(string name, string? description = null)
    {
        Guard.AgainstNullOrWhiteSpace(name, NameCannotBeEmpty);

        Name = name.Trim();
        Description = description?.Trim();
    }

    public void SetRetentionPeriod(TimeSpan period)
    {
        Guard.AgainstNegativeOrZero(period, RetentionPeriodMustBePositive);
        Guard.AgainstInvalidOperation(IsImmutable, CannotModifyImmutablePolicy);

        RetentionPeriod = period;
    }

    public void EnableLegalHold() => IsLegalHold = true;

    public void DisableLegalHold() => IsLegalHold = false;

    public void MakeImmutable() => IsImmutable = true;

    public void SetScope(string? scope) => Scope = scope?.Trim();

    public DateTimeOffset CalculateExpiryDate(DateTimeOffset archiveDate)
    {
        if (IsLegalHold) return DateTimeOffset.MaxValue;
        return archiveDate.Add(RetentionPeriod);
    }

    public bool IsExpired(DateTimeOffset archiveDate)
    {
        if (IsLegalHold) return false;
        return DateTimeOffset.UtcNow > CalculateExpiryDate(archiveDate);
    }
}
