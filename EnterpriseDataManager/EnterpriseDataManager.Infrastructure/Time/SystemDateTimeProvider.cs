namespace EnterpriseDataManager.Infrastructure.Time;

using EnterpriseDataManager.Application.Common.Interfaces;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTimeOffset Now => DateTimeOffset.Now;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
