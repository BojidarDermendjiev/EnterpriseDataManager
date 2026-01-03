namespace EnterpriseDataManager.Infrastructure.Logging;

public interface ISiemForwarder : IDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ForwardEventAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default);
    Task ForwardEventsAsync(IEnumerable<SecurityEvent> securityEvents, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<SiemForwarderStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task FlushAsync(CancellationToken cancellationToken = default);
}

public record SecurityEvent(
    string EventId,
    DateTimeOffset Timestamp,
    SecurityEventType EventType,
    SecuritySeverity Severity,
    string Source,
    string Message,
    string? UserId,
    string? SourceIp,
    string? DestinationIp,
    string? ResourcePath,
    string? Action,
    string? Outcome,
    Dictionary<string, object>? ExtendedProperties);

public record SiemForwarderStatus(
    bool IsConnected,
    DateTimeOffset? LastEventSentAt,
    long EventsSentTotal,
    long EventsFailedTotal,
    long EventsInQueue,
    TimeSpan? AverageLatency,
    string? LastError);

public enum SecurityEventType
{
    Authentication,
    Authorization,
    DataAccess,
    DataModification,
    DataDeletion,
    SystemConfiguration,
    NetworkActivity,
    MalwareDetection,
    PolicyViolation,
    AnomalyDetection,
    IntegrityViolation,
    Encryption,
    BackupRestore,
    UserManagement,
    SessionManagement,
    FileOperation,
    DatabaseOperation,
    ApiAccess,
    SystemHealth,
    SecurityAlert,
    AuditTrail
}

public enum SecuritySeverity
{
    Debug = 0,
    Informational = 1,
    Notice = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    Alert = 6,
    Emergency = 7
}
