namespace EnterpriseDataManager.Core.Interfaces.Services;

using EnterpriseDataManager.Core.Entities.Common;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    Task SendEmailAsync(IEnumerable<string> to, string subject, string body, CancellationToken cancellationToken = default);
    Task SendAlertAsync(AlertSeverity severity, string title, string message, CancellationToken cancellationToken = default);
    Task SendWebhookAsync(string webhookUrl, object payload, CancellationToken cancellationToken = default);
}

public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public interface ISchedulerService
{
    Task ScheduleJobAsync(Guid archivePlanId, DateTimeOffset executeAt, CancellationToken cancellationToken = default);
    Task CancelScheduledJobAsync(Guid archivePlanId, CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> GetNextExecutionTimeAsync(Guid archivePlanId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledJobInfo>> GetScheduledJobsAsync(CancellationToken cancellationToken = default);
    Task ProcessDueJobsAsync(CancellationToken cancellationToken = default);
}

public record ScheduledJobInfo(
    Guid ArchivePlanId,
    string PlanName,
    DateTimeOffset ScheduledAt,
    string? CronExpression);
