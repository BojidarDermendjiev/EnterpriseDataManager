namespace EnterpriseDataManager.Data;

public class ScheduledJobRecord
{
    public string JobId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public int JobType { get; set; }
    public string ParametersJson { get; set; } = "{}";
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
}
