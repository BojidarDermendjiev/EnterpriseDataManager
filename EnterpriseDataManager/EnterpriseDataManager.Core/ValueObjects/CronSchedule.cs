namespace EnterpriseDataManager.Core.ValueObjects;

using System.Text.RegularExpressions;
using EnterpriseDataManager.Common;
using static EnterpriseDataManager.Common.EntityValidationConstants;
public sealed class CronSchedule : ValueObject
{
    private static readonly Regex CronPattern = new(
        @"^(\*|([0-9]|[1-5][0-9])|\*\/[0-9]+)\s+" +
        @"(\*|([0-9]|1[0-9]|2[0-3])|\*\/[0-9]+)\s+" +
        @"(\*|([1-9]|[12][0-9]|3[01])|\*\/[0-9]+)\s+" +
        @"(\*|([1-9]|1[0-2])|\*\/[0-9]+)\s+" +
        @"(\*|[0-6]|\*\/[0-9]+)$",
        RegexOptions.Compiled);

    public string Expression { get; }
    public string? Description { get; }

    private CronSchedule(string expression, string? description = null)
    {
        Expression = expression;
        Description = description;
    }

    public static CronSchedule Create(string expression, string? description = null)
    {
        Guard.AgainstNullOrWhiteSpace(expression, CronExpressionCannotBeEmpty);

        var trimmed = expression.Trim();
        if (!CronPattern.IsMatch(trimmed))
            throw new ArgumentException(string.Format(InvalidCronExpression, expression), nameof(expression));

        return new CronSchedule(trimmed, description);
    }

    public static CronSchedule Daily(int hour = 0, int minute = 0)
        => new($"{minute} {hour} * * *", $"Daily at {hour:D2}:{minute:D2}");

    public static CronSchedule Weekly(DayOfWeek day, int hour = 0, int minute = 0)
        => new($"{minute} {hour} * * {(int)day}", $"Weekly on {day} at {hour:D2}:{minute:D2}");

    public static CronSchedule Monthly(int dayOfMonth, int hour = 0, int minute = 0)
        => new($"{minute} {hour} {dayOfMonth} * *", $"Monthly on day {dayOfMonth} at {hour:D2}:{minute:D2}");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Expression;
    }

    public override string ToString() => Description ?? Expression;
}
