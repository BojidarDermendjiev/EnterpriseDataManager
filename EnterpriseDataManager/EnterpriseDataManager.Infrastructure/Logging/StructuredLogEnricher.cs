namespace EnterpriseDataManager.Infrastructure.Logging;

using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;
using System.Diagnostics;

public class StructuredLogEnricher : ILogEventEnricher
{
    private readonly StructuredLogEnricherOptions _options;
    private static readonly AsyncLocal<LogContext?> _logContext = new();

    public StructuredLogEnricher(StructuredLogEnricherOptions? options = null)
    {
        _options = options ?? new StructuredLogEnricherOptions();
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Machine and process info
        if (_options.IncludeMachineName)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MachineName", Environment.MachineName));
        }

        if (_options.IncludeProcessInfo)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessId", Environment.ProcessId));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessName", Process.GetCurrentProcess().ProcessName));
        }

        // Thread info
        if (_options.IncludeThreadInfo)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ThreadId", Environment.CurrentManagedThreadId));
        }

        // Application info
        if (_options.IncludeApplicationInfo)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Application", _options.ApplicationName));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Environment", _options.EnvironmentName));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Version", _options.ApplicationVersion));
        }

        // Correlation info from async context
        var context = _logContext.Value;
        if (context != null)
        {
            if (!string.IsNullOrEmpty(context.CorrelationId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", context.CorrelationId));
            }

            if (!string.IsNullOrEmpty(context.RequestId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestId", context.RequestId));
            }

            if (!string.IsNullOrEmpty(context.UserId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", context.UserId));
            }

            if (!string.IsNullOrEmpty(context.SessionId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SessionId", context.SessionId));
            }

            if (!string.IsNullOrEmpty(context.TenantId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", context.TenantId));
            }

            if (!string.IsNullOrEmpty(context.SourceIp))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SourceIp", context.SourceIp));
            }

            if (!string.IsNullOrEmpty(context.UserAgent))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserAgent", context.UserAgent));
            }

            if (context.CustomProperties != null)
            {
                foreach (var (key, value) in context.CustomProperties)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(key, value));
                }
            }
        }

        // Timing info
        if (_options.IncludeTimingInfo && Activity.Current != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", Activity.Current.TraceId.ToString()));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", Activity.Current.SpanId.ToString()));

            if (Activity.Current.Parent != null)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ParentSpanId", Activity.Current.ParentSpanId.ToString()));
            }

            var duration = Activity.Current.Duration;
            if (duration > TimeSpan.Zero)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DurationMs", duration.TotalMilliseconds));
            }
        }
    }

    public static IDisposable BeginScope(LogContext context)
    {
        var previous = _logContext.Value;
        _logContext.Value = context;
        return new LogContextScope(previous);
    }

    public static IDisposable BeginScope(string correlationId, string? userId = null, string? requestId = null)
    {
        return BeginScope(new LogContext
        {
            CorrelationId = correlationId,
            UserId = userId,
            RequestId = requestId
        });
    }

    public static void SetCorrelationId(string correlationId)
    {
        var context = _logContext.Value ?? new LogContext();
        context = context with { CorrelationId = correlationId };
        _logContext.Value = context;
    }

    public static void SetUserId(string userId)
    {
        var context = _logContext.Value ?? new LogContext();
        context = context with { UserId = userId };
        _logContext.Value = context;
    }

    public static void SetRequestId(string requestId)
    {
        var context = _logContext.Value ?? new LogContext();
        context = context with { RequestId = requestId };
        _logContext.Value = context;
    }

    public static void AddCustomProperty(string key, object value)
    {
        var context = _logContext.Value ?? new LogContext();
        context.CustomProperties ??= new Dictionary<string, object>();
        context.CustomProperties[key] = value;
        _logContext.Value = context;
    }

    public static LogContext? GetCurrentContext()
    {
        return _logContext.Value;
    }

    private class LogContextScope : IDisposable
    {
        private readonly LogContext? _previous;

        public LogContextScope(LogContext? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            _logContext.Value = _previous;
        }
    }
}

public class StructuredLogEnricherOptions
{
    public string ApplicationName { get; set; } = "EnterpriseDataManager";
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationVersion { get; set; } = "1.0.0";
    public bool IncludeMachineName { get; set; } = true;
    public bool IncludeProcessInfo { get; set; } = true;
    public bool IncludeThreadInfo { get; set; } = true;
    public bool IncludeApplicationInfo { get; set; } = true;
    public bool IncludeTimingInfo { get; set; } = true;
}

public record LogContext
{
    public string? CorrelationId { get; init; }
    public string? RequestId { get; init; }
    public string? UserId { get; init; }
    public string? SessionId { get; init; }
    public string? TenantId { get; init; }
    public string? SourceIp { get; init; }
    public string? UserAgent { get; init; }
    public Dictionary<string, object>? CustomProperties { get; set; }
}

public static class LoggerConfigurationExtensions
{
    public static Serilog.LoggerConfiguration WithStructuredEnrichment(
        this Serilog.Configuration.LoggerEnrichmentConfiguration enrichmentConfiguration,
        StructuredLogEnricherOptions? options = null)
    {
        return enrichmentConfiguration.With(new StructuredLogEnricher(options));
    }
}

public class AuditLogEntry
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? SourceIp { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Outcome { get; set; }
    public string? Reason { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public interface IAuditLogger
{
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task LogAsync(string eventType, string action, string? resourceType = null, string? resourceId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> GetEntriesAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}

public class AuditLogQuery
{
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string? UserId { get; set; }
    public string? EventType { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}

public class InMemoryAuditLogger : IAuditLogger
{
    private readonly ConcurrentQueue<AuditLogEntry> _entries = new();
    private readonly int _maxEntries;

    public InMemoryAuditLogger(int maxEntries = 10000)
    {
        _maxEntries = maxEntries;
    }

    public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        var context = StructuredLogEnricher.GetCurrentContext();

        entry.UserId ??= context?.UserId;
        entry.SourceIp ??= context?.SourceIp;

        if (entry.Metadata == null)
        {
            entry.Metadata = new Dictionary<string, object>();
        }

        if (!string.IsNullOrEmpty(context?.CorrelationId))
        {
            entry.Metadata["CorrelationId"] = context.CorrelationId;
        }

        if (!string.IsNullOrEmpty(context?.RequestId))
        {
            entry.Metadata["RequestId"] = context.RequestId;
        }

        _entries.Enqueue(entry);

        while (_entries.Count > _maxEntries)
        {
            _entries.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    public Task LogAsync(
        string eventType,
        string action,
        string? resourceType = null,
        string? resourceId = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLogEntry
        {
            EventType = eventType,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Outcome = "Success"
        };

        return LogAsync(entry, cancellationToken);
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetEntriesAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var entries = _entries.AsEnumerable();

        if (query.StartTime.HasValue)
        {
            entries = entries.Where(e => e.Timestamp >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            entries = entries.Where(e => e.Timestamp <= query.EndTime.Value);
        }

        if (!string.IsNullOrEmpty(query.UserId))
        {
            entries = entries.Where(e => e.UserId == query.UserId);
        }

        if (!string.IsNullOrEmpty(query.EventType))
        {
            entries = entries.Where(e => e.EventType == query.EventType);
        }

        if (!string.IsNullOrEmpty(query.ResourceType))
        {
            entries = entries.Where(e => e.ResourceType == query.ResourceType);
        }

        if (!string.IsNullOrEmpty(query.ResourceId))
        {
            entries = entries.Where(e => e.ResourceId == query.ResourceId);
        }

        var result = entries
            .OrderByDescending(e => e.Timestamp)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(result);
    }
}
