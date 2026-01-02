namespace EnterpriseDataManager.Application.Common.Interfaces;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset Now { get; }
    DateOnly Today { get; }
}
