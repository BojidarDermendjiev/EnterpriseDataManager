namespace EnterpriseDataManager.Infrastructure.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

public class CefForwarderOptions
{
    public const string SectionName = "CefForwarder";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 514;
    public CefTransport Transport { get; set; } = CefTransport.Udp;
    public string DeviceVendor { get; set; } = "EnterpriseDataManager";
    public string DeviceProduct { get; set; } = "EDM";
    public string DeviceVersion { get; set; } = "1.0";
    public int CefVersion { get; set; } = 0;
    public int BatchSize { get; set; } = 100;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxQueueSize { get; set; } = 10000;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 3;
}

public class CefForwarder : ISiemForwarder
{
    private readonly CefForwarderOptions _options;
    private readonly ILogger<CefForwarder>? _logger;
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

    public CefForwarder(
        IOptions<CefForwarderOptions> options,
        ILogger<CefForwarder>? logger = null)
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
            if (_options.Transport == CefTransport.Udp)
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
            _logger?.LogInformation("CEF forwarder initialized. Target: {Host}:{Port} ({Transport})",
                _options.Host, _options.Port, _options.Transport);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger?.LogError(ex, "Failed to initialize CEF forwarder");
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
                var cefMessage = FormatCefMessage(securityEvent);
                var bytes = Encoding.UTF8.GetBytes(cefMessage);

                await SendAsync(bytes, cancellationToken);

                Interlocked.Increment(ref _eventsSentTotal);
                _lastEventSentAt = DateTimeOffset.UtcNow;

                stopwatch.Stop();
                RecordLatency(stopwatch.ElapsedMilliseconds);

                _logger?.LogDebug("Forwarded event {EventId} to CEF", securityEvent.EventId);
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
            _logger?.LogError(ex, "Failed to forward event {EventId} to CEF", securityEvent.EventId);
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

            var testEvent = new SecurityEvent(
                EventId: Guid.NewGuid().ToString(),
                Timestamp: DateTimeOffset.UtcNow,
                EventType: SecurityEventType.SystemHealth,
                Severity: SecuritySeverity.Informational,
                Source: _options.DeviceProduct,
                Message: "CEF connection test",
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
            _logger?.LogError(ex, "CEF connection test failed");
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

    private string FormatCefMessage(SecurityEvent evt)
    {
        // CEF format:
        // CEF:Version|Device Vendor|Device Product|Device Version|Device Event Class ID|Name|Severity|[Extension]
        var severity = MapCefSeverity(evt.Severity);
        var eventClassId = GetEventClassId(evt.EventType);
        var extension = BuildCefExtension(evt);

        var header = $"CEF:{_options.CefVersion}|{EscapeCefHeader(_options.DeviceVendor)}|{EscapeCefHeader(_options.DeviceProduct)}|{EscapeCefHeader(_options.DeviceVersion)}|{eventClassId}|{EscapeCefHeader(evt.EventType.ToString())}|{severity}|";

        return header + extension;
    }

    private static int MapCefSeverity(SecuritySeverity severity)
    {
        // CEF severity: 0-3 Low, 4-6 Medium, 7-8 High, 9-10 Very High
        return severity switch
        {
            SecuritySeverity.Debug => 0,
            SecuritySeverity.Informational => 1,
            SecuritySeverity.Notice => 2,
            SecuritySeverity.Warning => 4,
            SecuritySeverity.Error => 6,
            SecuritySeverity.Critical => 8,
            SecuritySeverity.Alert => 9,
            SecuritySeverity.Emergency => 10,
            _ => 1
        };
    }

    private static string GetEventClassId(SecurityEventType eventType)
    {
        // Map event types to numeric class IDs
        return eventType switch
        {
            SecurityEventType.Authentication => "100",
            SecurityEventType.Authorization => "110",
            SecurityEventType.DataAccess => "200",
            SecurityEventType.DataModification => "210",
            SecurityEventType.DataDeletion => "220",
            SecurityEventType.SystemConfiguration => "300",
            SecurityEventType.NetworkActivity => "400",
            SecurityEventType.MalwareDetection => "500",
            SecurityEventType.PolicyViolation => "600",
            SecurityEventType.AnomalyDetection => "700",
            SecurityEventType.IntegrityViolation => "800",
            SecurityEventType.Encryption => "900",
            SecurityEventType.BackupRestore => "1000",
            SecurityEventType.UserManagement => "1100",
            SecurityEventType.SessionManagement => "1200",
            SecurityEventType.FileOperation => "1300",
            SecurityEventType.DatabaseOperation => "1400",
            SecurityEventType.ApiAccess => "1500",
            SecurityEventType.SystemHealth => "1600",
            SecurityEventType.SecurityAlert => "1700",
            SecurityEventType.AuditTrail => "1800",
            _ => "9999"
        };
    }

    private string BuildCefExtension(SecurityEvent evt)
    {
        var sb = new StringBuilder();

        // Standard CEF extension fields
        AddCefField(sb, "rt", evt.Timestamp.ToUnixTimeMilliseconds().ToString());
        AddCefField(sb, "msg", evt.Message);
        AddCefField(sb, "externalId", evt.EventId);
        AddCefField(sb, "cat", evt.EventType.ToString());
        AddCefField(sb, "dhost", Environment.MachineName);

        if (!string.IsNullOrEmpty(evt.UserId))
            AddCefField(sb, "suser", evt.UserId);

        if (!string.IsNullOrEmpty(evt.SourceIp))
            AddCefField(sb, "src", evt.SourceIp);

        if (!string.IsNullOrEmpty(evt.DestinationIp))
            AddCefField(sb, "dst", evt.DestinationIp);

        if (!string.IsNullOrEmpty(evt.ResourcePath))
            AddCefField(sb, "filePath", evt.ResourcePath);

        if (!string.IsNullOrEmpty(evt.Action))
            AddCefField(sb, "act", evt.Action);

        if (!string.IsNullOrEmpty(evt.Outcome))
            AddCefField(sb, "outcome", evt.Outcome);

        // Extended properties as custom fields
        if (evt.ExtendedProperties != null)
        {
            var customIndex = 1;
            foreach (var (key, value) in evt.ExtendedProperties)
            {
                if (customIndex <= 6)
                {
                    AddCefField(sb, $"cs{customIndex}", value?.ToString() ?? "");
                    AddCefField(sb, $"cs{customIndex}Label", key);
                    customIndex++;
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static void AddCefField(StringBuilder sb, string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            sb.Append($"{key}={EscapeCefExtension(value)} ");
        }
    }

    private static string EscapeCefHeader(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("|", "\\|");
    }

    private static string EscapeCefExtension(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("=", "\\=")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    private async Task SendAsync(byte[] data, CancellationToken cancellationToken)
    {
        var retryCount = 0;

        while (retryCount <= _options.MaxRetries)
        {
            try
            {
                if (_options.Transport == CefTransport.Udp && _udpClient != null)
                {
                    await _udpClient.SendAsync(data, data.Length);
                }
                else if (_tcpStream != null)
                {
                    await _tcpStream.WriteAsync(data, cancellationToken);
                    await _tcpStream.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
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

                _logger?.LogWarning(ex, "CEF send attempt {Attempt} failed, retrying...", retryCount);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * retryCount), cancellationToken);

                if (_options.Transport == CefTransport.Tcp)
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

            _logger?.LogInformation("Reconnected to CEF server");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reconnect to CEF server");
            throw;
        }
    }

    private void RecordLatency(long latencyMs)
    {
        lock (_latencyLock)
        {
            _latencies.Add(latencyMs);

            while (_latencies.Count > 1000)
            {
                _latencies.RemoveAt(0);
            }
        }
    }
}

public enum CefTransport
{
    Udp,
    Tcp,
    TcpTls
}
