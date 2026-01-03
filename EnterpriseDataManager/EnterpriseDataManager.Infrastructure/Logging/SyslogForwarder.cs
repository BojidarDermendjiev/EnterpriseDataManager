namespace EnterpriseDataManager.Infrastructure.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class SyslogForwarderOptions
{
    public const string SectionName = "SyslogForwarder";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 514;
    public SyslogProtocol Protocol { get; set; } = SyslogProtocol.Udp;
    public SyslogFormat Format { get; set; } = SyslogFormat.Rfc5424;
    public string AppName { get; set; } = "EnterpriseDataManager";
    public string Facility { get; set; } = "local0";
    public bool EnableTls { get; set; } = false;
    public int BatchSize { get; set; } = 100;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxQueueSize { get; set; } = 10000;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 3;
}

public class SyslogForwarder : ISiemForwarder
{
    private readonly SyslogForwarderOptions _options;
    private readonly ILogger<SyslogForwarder>? _logger;
    private readonly ConcurrentQueue<SecurityEvent> _eventQueue = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private UdpClient? _udpClient;
    private TcpClient? _tcpClient;
    private NetworkStream? _tcpStream;
    private bool _isInitialized;
    private bool _isDisposed;
    private long _eventsSentTotal;
    private long _eventsFailedTotal;
    private DateTimeOffset? _lastEventSentAt;
    private string? _lastError;
    private readonly List<long> _latencies = new();
    private readonly object _latencyLock = new();

    public SyslogForwarder(
        IOptions<SyslogForwarderOptions> options,
        ILogger<SyslogForwarder>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        try
        {
            if (_options.Protocol == SyslogProtocol.Udp)
            {
                _udpClient = new UdpClient();
                _udpClient.Connect(_options.Host, _options.Port);
            }
            else
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_options.Host, _options.Port, cancellationToken);
                _tcpStream = _tcpClient.GetStream();
            }

            _isInitialized = true;
            _logger?.LogInformation("Syslog forwarder initialized. Target: {Host}:{Port} ({Protocol})",
                _options.Host, _options.Port, _options.Protocol);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger?.LogError(ex, "Failed to initialize Syslog forwarder");
            throw;
        }
    }

    public async Task ForwardEventAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _sendSemaphore.WaitAsync(cancellationToken);

            try
            {
                var message = FormatSyslogMessage(securityEvent);
                var bytes = Encoding.UTF8.GetBytes(message);

                await SendAsync(bytes, cancellationToken);

                Interlocked.Increment(ref _eventsSentTotal);
                _lastEventSentAt = DateTimeOffset.UtcNow;

                stopwatch.Stop();
                RecordLatency(stopwatch.ElapsedMilliseconds);

                _logger?.LogDebug("Forwarded event {EventId} to Syslog", securityEvent.EventId);
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _eventsFailedTotal);
            _lastError = ex.Message;
            _logger?.LogError(ex, "Failed to forward event {EventId}", securityEvent.EventId);
            throw;
        }
    }

    public async Task ForwardEventsAsync(IEnumerable<SecurityEvent> securityEvents, CancellationToken cancellationToken = default)
    {
        foreach (var batch in securityEvents.Chunk(_options.BatchSize))
        {
            foreach (var evt in batch)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await ForwardEventAsync(evt, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to forward event {EventId}, continuing with batch", evt.EventId);
                }
            }
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isInitialized)
            {
                await InitializeAsync(cancellationToken);
            }

            // Send a test message
            var testEvent = new SecurityEvent(
                EventId: Guid.NewGuid().ToString(),
                Timestamp: DateTimeOffset.UtcNow,
                EventType: SecurityEventType.SystemHealth,
                Severity: SecuritySeverity.Informational,
                Source: _options.AppName,
                Message: "Syslog connection test",
                UserId: null,
                SourceIp: null,
                DestinationIp: null,
                ResourcePath: null,
                Action: "test",
                Outcome: "success",
                ExtendedProperties: null);

            await ForwardEventAsync(testEvent, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Syslog connection test failed");
            return false;
        }
    }

    public Task<SiemForwarderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        TimeSpan? avgLatency = null;

        lock (_latencyLock)
        {
            if (_latencies.Count > 0)
            {
                avgLatency = TimeSpan.FromMilliseconds(_latencies.Average());
            }
        }

        var status = new SiemForwarderStatus(
            IsConnected: _isInitialized && (_udpClient != null || (_tcpClient?.Connected ?? false)),
            LastEventSentAt: _lastEventSentAt,
            EventsSentTotal: Interlocked.Read(ref _eventsSentTotal),
            EventsFailedTotal: Interlocked.Read(ref _eventsFailedTotal),
            EventsInQueue: _eventQueue.Count,
            AverageLatency: avgLatency,
            LastError: _lastError);

        return Task.FromResult(status);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // Process any queued events
        while (_eventQueue.TryDequeue(out var evt) && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                ForwardEventAsync(evt, cancellationToken).Wait(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to flush event {EventId}", evt.EventId);
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _udpClient?.Dispose();
        _tcpStream?.Dispose();
        _tcpClient?.Dispose();
        _sendSemaphore.Dispose();

        _isDisposed = true;
    }

    private string FormatSyslogMessage(SecurityEvent evt)
    {
        return _options.Format switch
        {
            SyslogFormat.Rfc5424 => FormatRfc5424(evt),
            SyslogFormat.Rfc3164 => FormatRfc3164(evt),
            _ => FormatRfc5424(evt)
        };
    }

    private string FormatRfc5424(SecurityEvent evt)
    {
        // RFC 5424 format:
        // <PRI>VERSION TIMESTAMP HOSTNAME APP-NAME PROCID MSGID [SD-ID SD-PARAM...] MSG
        var priority = CalculatePriority(evt.Severity);
        var timestamp = evt.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.ffffffK");
        var hostname = Environment.MachineName;
        var procId = Environment.ProcessId.ToString();
        var msgId = evt.EventType.ToString();

        // Structured data
        var structuredData = BuildStructuredData(evt);

        return $"<{priority}>1 {timestamp} {hostname} {_options.AppName} {procId} {msgId} {structuredData} {evt.Message}";
    }

    private string FormatRfc3164(SecurityEvent evt)
    {
        // RFC 3164 format:
        // <PRI>TIMESTAMP HOSTNAME TAG: MSG
        var priority = CalculatePriority(evt.Severity);
        var timestamp = evt.Timestamp.ToString("MMM dd HH:mm:ss");
        var hostname = Environment.MachineName;

        return $"<{priority}>{timestamp} {hostname} {_options.AppName}: {evt.Message}";
    }

    private int CalculatePriority(SecuritySeverity severity)
    {
        var facility = _options.Facility switch
        {
            "kern" => 0,
            "user" => 1,
            "mail" => 2,
            "daemon" => 3,
            "auth" => 4,
            "syslog" => 5,
            "lpr" => 6,
            "news" => 7,
            "uucp" => 8,
            "cron" => 9,
            "authpriv" => 10,
            "ftp" => 11,
            "local0" => 16,
            "local1" => 17,
            "local2" => 18,
            "local3" => 19,
            "local4" => 20,
            "local5" => 21,
            "local6" => 22,
            "local7" => 23,
            _ => 16 // default to local0
        };

        return (facility * 8) + (int)severity;
    }

    private string BuildStructuredData(SecurityEvent evt)
    {
        var sb = new StringBuilder();

        // Event metadata
        sb.Append($"[event@0 eventId=\"{EscapeStructuredData(evt.EventId)}\" eventType=\"{evt.EventType}\"");

        if (!string.IsNullOrEmpty(evt.UserId))
            sb.Append($" userId=\"{EscapeStructuredData(evt.UserId)}\"");

        if (!string.IsNullOrEmpty(evt.SourceIp))
            sb.Append($" sourceIp=\"{EscapeStructuredData(evt.SourceIp)}\"");

        if (!string.IsNullOrEmpty(evt.DestinationIp))
            sb.Append($" destinationIp=\"{EscapeStructuredData(evt.DestinationIp)}\"");

        if (!string.IsNullOrEmpty(evt.ResourcePath))
            sb.Append($" resourcePath=\"{EscapeStructuredData(evt.ResourcePath)}\"");

        if (!string.IsNullOrEmpty(evt.Action))
            sb.Append($" action=\"{EscapeStructuredData(evt.Action)}\"");

        if (!string.IsNullOrEmpty(evt.Outcome))
            sb.Append($" outcome=\"{EscapeStructuredData(evt.Outcome)}\"");

        sb.Append(']');

        // Extended properties
        if (evt.ExtendedProperties != null && evt.ExtendedProperties.Count > 0)
        {
            sb.Append(" [ext@0");
            foreach (var (key, value) in evt.ExtendedProperties)
            {
                sb.Append($" {EscapeStructuredData(key)}=\"{EscapeStructuredData(value?.ToString() ?? "")}\"");
            }
            sb.Append(']');
        }

        return sb.ToString();
    }

    private static string EscapeStructuredData(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("]", "\\]");
    }

    private async Task SendAsync(byte[] data, CancellationToken cancellationToken)
    {
        var retryCount = 0;

        while (retryCount <= _options.MaxRetries)
        {
            try
            {
                if (_options.Protocol == SyslogProtocol.Udp && _udpClient != null)
                {
                    await _udpClient.SendAsync(data, data.Length);
                }
                else if (_tcpStream != null)
                {
                    // TCP requires message framing (RFC 6587)
                    var framedData = Encoding.UTF8.GetBytes($"{data.Length} ").Concat(data).ToArray();
                    await _tcpStream.WriteAsync(framedData, cancellationToken);
                }

                return;
            }
            catch (Exception ex)
            {
                retryCount++;

                if (retryCount > _options.MaxRetries)
                {
                    throw;
                }

                _logger?.LogWarning(ex, "Syslog send attempt {Attempt} failed, retrying...", retryCount);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * retryCount), cancellationToken);

                // Reconnect if needed
                if (_options.Protocol == SyslogProtocol.Tcp)
                {
                    await ReconnectTcpAsync(cancellationToken);
                }
            }
        }
    }

    private async Task ReconnectTcpAsync(CancellationToken cancellationToken)
    {
        try
        {
            _tcpStream?.Dispose();
            _tcpClient?.Dispose();

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_options.Host, _options.Port, cancellationToken);
            _tcpStream = _tcpClient.GetStream();

            _logger?.LogInformation("Reconnected to Syslog server");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reconnect to Syslog server");
            throw;
        }
    }

    private void RecordLatency(long latencyMs)
    {
        lock (_latencyLock)
        {
            _latencies.Add(latencyMs);

            // Keep only recent latencies
            while (_latencies.Count > 1000)
            {
                _latencies.RemoveAt(0);
            }
        }
    }
}

public enum SyslogProtocol
{
    Udp,
    Tcp,
    TcpTls
}

public enum SyslogFormat
{
    Rfc3164,
    Rfc5424
}
