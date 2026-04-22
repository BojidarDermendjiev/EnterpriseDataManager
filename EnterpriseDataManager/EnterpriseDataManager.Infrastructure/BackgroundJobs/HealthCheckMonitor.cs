namespace EnterpriseDataManager.Infrastructure.BackgroundJobs;

using EnterpriseDataManager.Data;
using EnterpriseDataManager.Infrastructure.ExternalServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;

public class HealthCheckMonitorOptions
{
    public const string SectionName = "HealthCheckMonitor";

    public bool Enabled { get; set; } = true;
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ComponentTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int UnhealthyThreshold { get; set; } = 3;
    public int HealthyThreshold { get; set; } = 2;
    public bool EnableAlerts { get; set; } = true;
    public List<HealthCheckComponentConfiguration> Components { get; set; } = new();
}

public class HealthCheckComponentConfiguration
{
    public string Name { get; set; } = string.Empty;
    public ComponentType Type { get; set; }
    public string? ConnectionString { get; set; }
    public string? Endpoint { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsCritical { get; set; } = false;
}

public class HealthCheckMonitor : BackgroundService, IHealthCheckMonitor
{
    private readonly HealthCheckMonitorOptions _options;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<HealthCheckMonitor>? _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ConcurrentDictionary<string, ComponentHealth> _componentHealth = new();
    private readonly ConcurrentQueue<HealthCheckResult> _healthHistory = new();
    private readonly ConcurrentDictionary<string, int> _consecutiveFailures = new();
    private readonly ConcurrentDictionary<string, int> _consecutiveSuccesses = new();
    private OverallHealthStatus _currentStatus = OverallHealthStatus.Unknown;

    public HealthCheckMonitor(
        IOptions<HealthCheckMonitorOptions> options,
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        INotificationService? notificationService = null,
        ILogger<HealthCheckMonitor>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _notificationService = notificationService;
        _logger = logger;

        InitializeComponents();
    }

    public Task<OverallHealthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentStatus);
    }

    public Task<IReadOnlyDictionary<string, ComponentHealth>> GetComponentHealthAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<string, ComponentHealth>>(
            new Dictionary<string, ComponentHealth>(_componentHealth));
    }

    public Task<ComponentHealth?> GetComponentHealthAsync(
        string componentName,
        CancellationToken cancellationToken = default)
    {
        _componentHealth.TryGetValue(componentName, out var health);
        return Task.FromResult(health);
    }

    public Task<IReadOnlyList<HealthCheckResult>> GetHealthHistoryAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        var history = _healthHistory
            .OrderByDescending(r => r.CheckedAt)
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<HealthCheckResult>>(history);
    }

    public async Task<HealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var componentResults = new Dictionary<string, ComponentCheckResult>();

        _logger?.LogInformation("Starting health check");

        foreach (var component in _componentHealth.Keys)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var result = await CheckComponentAsync(component, cancellationToken);
                componentResults[component] = result;

                UpdateComponentHealth(component, result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking component {Component}", component);

                var errorResult = new ComponentCheckResult(
                    ComponentName: component,
                    Status: ComponentStatus.Unhealthy,
                    Message: ex.Message,
                    ResponseTimeMs: -1,
                    CheckedAt: DateTimeOffset.UtcNow,
                    Metadata: null);

                componentResults[component] = errorResult;
                UpdateComponentHealth(component, errorResult);
            }
        }

        stopwatch.Stop();

        var overallStatus = DetermineOverallStatus(componentResults.Values);
        _currentStatus = overallStatus;

        var healthResult = new HealthCheckResult(
            CheckId: Guid.NewGuid().ToString(),
            CheckedAt: startTime,
            Duration: stopwatch.Elapsed,
            OverallStatus: overallStatus,
            ComponentResults: componentResults,
            TotalComponents: componentResults.Count,
            HealthyComponents: componentResults.Values.Count(r => r.Status == ComponentStatus.Healthy),
            UnhealthyComponents: componentResults.Values.Count(r => r.Status == ComponentStatus.Unhealthy),
            DegradedComponents: componentResults.Values.Count(r => r.Status == ComponentStatus.Degraded));

        _healthHistory.Enqueue(healthResult);

        while (_healthHistory.Count > 1000)
        {
            _healthHistory.TryDequeue(out _);
        }

        _logger?.LogInformation("Health check completed. Status: {Status}, Healthy: {Healthy}/{Total}",
            overallStatus, healthResult.HealthyComponents, healthResult.TotalComponents);

        // Send alerts if needed
        if (_options.EnableAlerts && overallStatus != OverallHealthStatus.Healthy)
        {
            await SendHealthAlertAsync(healthResult, cancellationToken);
        }

        return healthResult;
    }

    public Task<string> RegisterComponentAsync(
        string name,
        ComponentType type,
        string? connectionString = null,
        string? endpoint = null,
        bool isCritical = false,
        CancellationToken cancellationToken = default)
    {
        var componentId = name;

        var health = new ComponentHealth(
            ComponentName: name,
            Type: type,
            Status: ComponentStatus.Unknown,
            LastCheckedAt: null,
            LastHealthyAt: null,
            ConsecutiveFailures: 0,
            IsCritical: isCritical,
            ConnectionString: connectionString,
            Endpoint: endpoint);

        _componentHealth[componentId] = health;
        _consecutiveFailures[componentId] = 0;
        _consecutiveSuccesses[componentId] = 0;

        _logger?.LogInformation("Registered health check component {Component} (Type: {Type}, Critical: {Critical})",
            name, type, isCritical);

        return Task.FromResult(componentId);
    }

    public Task<bool> UnregisterComponentAsync(string componentName, CancellationToken cancellationToken = default)
    {
        var removed = _componentHealth.TryRemove(componentName, out _);
        _consecutiveFailures.TryRemove(componentName, out _);
        _consecutiveSuccesses.TryRemove(componentName, out _);

        if (removed)
        {
            _logger?.LogInformation("Unregistered health check component {Component}", componentName);
        }

        return Task.FromResult(removed);
    }

    public Task<HealthSummary> GetHealthSummaryAsync(CancellationToken cancellationToken = default)
    {
        var recentHistory = _healthHistory
            .Where(h => h.CheckedAt >= DateTimeOffset.UtcNow.AddHours(-24))
            .ToList();

        var summary = new HealthSummary(
            CurrentStatus: _currentStatus,
            TotalComponents: _componentHealth.Count,
            HealthyComponents: _componentHealth.Values.Count(c => c.Status == ComponentStatus.Healthy),
            UnhealthyComponents: _componentHealth.Values.Count(c => c.Status == ComponentStatus.Unhealthy),
            DegradedComponents: _componentHealth.Values.Count(c => c.Status == ComponentStatus.Degraded),
            CriticalComponentsDown: _componentHealth.Values.Count(c => c.IsCritical && c.Status == ComponentStatus.Unhealthy),
            UptimePercentage24h: CalculateUptimePercentage(recentHistory),
            AverageResponseTimeMs: CalculateAverageResponseTime(recentHistory),
            LastCheckAt: _healthHistory.LastOrDefault()?.CheckedAt,
            StatusChanges24h: CountStatusChanges(recentHistory));

        return Task.FromResult(summary);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger?.LogInformation("Health check monitor is disabled");
            return;
        }

        _logger?.LogInformation("Health check monitor started with interval {Interval}", _options.CheckInterval);

        // Perform initial check
        await PerformHealthCheckAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_options.CheckInterval, stoppingToken);

            try
            {
                await PerformHealthCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during health check");
            }
        }

        _logger?.LogInformation("Health check monitor stopped");
    }

    private void InitializeComponents()
    {
        foreach (var config in _options.Components.Where(c => c.IsEnabled))
        {
            var health = new ComponentHealth(
                ComponentName: config.Name,
                Type: config.Type,
                Status: ComponentStatus.Unknown,
                LastCheckedAt: null,
                LastHealthyAt: null,
                ConsecutiveFailures: 0,
                IsCritical: config.IsCritical,
                ConnectionString: config.ConnectionString,
                Endpoint: config.Endpoint);

            _componentHealth[config.Name] = health;
            _consecutiveFailures[config.Name] = 0;
            _consecutiveSuccesses[config.Name] = 0;
        }
    }

    private async Task<ComponentCheckResult> CheckComponentAsync(
        string componentName,
        CancellationToken cancellationToken)
    {
        if (!_componentHealth.TryGetValue(componentName, out var component))
        {
            return new ComponentCheckResult(
                ComponentName: componentName,
                Status: ComponentStatus.Unknown,
                Message: "Component not found",
                ResponseTimeMs: -1,
                CheckedAt: DateTimeOffset.UtcNow,
                Metadata: null);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.ComponentTimeout);

            var (isHealthy, message, metadata) = await PerformComponentCheckAsync(component, timeoutCts.Token);

            stopwatch.Stop();

            return new ComponentCheckResult(
                ComponentName: componentName,
                Status: isHealthy ? ComponentStatus.Healthy : ComponentStatus.Unhealthy,
                Message: message,
                ResponseTimeMs: stopwatch.ElapsedMilliseconds,
                CheckedAt: DateTimeOffset.UtcNow,
                Metadata: metadata);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();

            return new ComponentCheckResult(
                ComponentName: componentName,
                Status: ComponentStatus.Unhealthy,
                Message: "Health check timed out",
                ResponseTimeMs: stopwatch.ElapsedMilliseconds,
                CheckedAt: DateTimeOffset.UtcNow,
                Metadata: null);
        }
    }

    private async Task<(bool IsHealthy, string Message, Dictionary<string, object>? Metadata)> PerformComponentCheckAsync(
        ComponentHealth component,
        CancellationToken cancellationToken)
    {
        return component.Type switch
        {
            ComponentType.Database => await CheckDatabaseAsync(component, cancellationToken),
            ComponentType.Storage => await CheckStorageAsync(component, cancellationToken),
            ComponentType.Cache => await CheckCacheAsync(component, cancellationToken),
            ComponentType.MessageQueue => await CheckMessageQueueAsync(component, cancellationToken),
            ComponentType.ExternalApi => await CheckExternalApiAsync(component, cancellationToken),
            ComponentType.FileSystem => await CheckFileSystemAsync(component, cancellationToken),
            ComponentType.Network => await CheckNetworkAsync(component, cancellationToken),
            _ => (true, "Unknown component type - assumed healthy", null)
        };
    }

    private async Task<(bool, string, Dictionary<string, object>?)> CheckDatabaseAsync(
        ComponentHealth component,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

            var sw = Stopwatch.StartNew();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            sw.Stop();

            var metadata = new Dictionary<string, object>
            {
                ["response_ms"] = sw.ElapsedMilliseconds,
                ["provider"] = db.Database.ProviderName ?? "unknown"
            };

            return canConnect
                ? (true, "Database connection successful", metadata)
                : (false, "Database CanConnect returned false", metadata);
        }
        catch (Exception ex)
        {
            return (false, $"Database check failed: {ex.Message}", null);
        }
    }

    private Task<(bool, string, Dictionary<string, object>?)> CheckStorageAsync(
        ComponentHealth component,
        CancellationToken cancellationToken)
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new
                {
                    d.Name,
                    FreeGb = Math.Round(d.AvailableFreeSpace / (1024.0 * 1024 * 1024), 2),
                    TotalGb = Math.Round(d.TotalSize / (1024.0 * 1024 * 1024), 2),
                    UsagePct = Math.Round((1.0 - (double)d.AvailableFreeSpace / d.TotalSize) * 100, 1)
                })
                .ToList();

            var criticalDrive = drives.FirstOrDefault(d => d.UsagePct > 90);
            var metadata = new Dictionary<string, object>
            {
                ["drives"] = drives.Count,
                ["total_free_gb"] = drives.Sum(d => d.FreeGb),
                ["usage_pct_max"] = drives.Any() ? drives.Max(d => d.UsagePct) : 0
            };

            return criticalDrive is not null
                ? Task.FromResult((false, $"Drive {criticalDrive.Name} usage at {criticalDrive.UsagePct}%", (Dictionary<string, object>?)metadata))
                : Task.FromResult((true, "Storage healthy", (Dictionary<string, object>?)metadata));
        }
        catch (Exception ex)
        {
            return Task.FromResult<(bool, string, Dictionary<string, object>?)>((false, $"Storage check failed: {ex.Message}", null));
        }
    }

    private Task<(bool, string, Dictionary<string, object>?)> CheckCacheAsync(
        ComponentHealth component,
        CancellationToken cancellationToken)
    {
        try
        {
            const string testKey = "_healthcheck_probe";
            const string testValue = "ok";

            _memoryCache.Set(testKey, testValue, TimeSpan.FromSeconds(5));
            var retrieved = _memoryCache.TryGetValue(testKey, out string? val);

            var isHealthy = retrieved && val == testValue;
            var metadata = new Dictionary<string, object> { ["probe"] = isHealthy ? "pass" : "fail" };

            return Task.FromResult(isHealthy
                ? (true, "Cache operational", (Dictionary<string, object>?)metadata)
                : (false, "Cache probe failed — set/get mismatch", (Dictionary<string, object>?)metadata));
        }
        catch (Exception ex)
        {
            return Task.FromResult<(bool, string, Dictionary<string, object>?)>((false, $"Cache check failed: {ex.Message}", null));
        }
    }

    private Task<(bool, string, Dictionary<string, object>?)> CheckMessageQueueAsync(
        ComponentHealth component,
        CancellationToken cancellationToken)
    {
        // No message queue is configured in this application — report as not applicable
        var metadata = new Dictionary<string, object> { ["status"] = "not_configured" };
        return Task.FromResult((true, "No message queue configured", (Dictionary<string, object>?)metadata));
    }

    private async Task<(bool, string, Dictionary<string, object>?)> CheckExternalApiAsync(
        ComponentHealth component,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(component.Endpoint))
        {
            return (false, "No endpoint configured", null);
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = _options.ComponentTimeout;

            var response = await httpClient.GetAsync(component.Endpoint, cancellationToken);

            var metadata = new Dictionary<string, object>
            {
                ["status_code"] = (int)response.StatusCode
            };

            return response.IsSuccessStatusCode
                ? (true, $"API responded with {response.StatusCode}", metadata)
                : (false, $"API returned {response.StatusCode}", metadata);
        }
        catch (Exception ex)
        {
            return (false, $"API check failed: {ex.Message}", null);
        }
    }

    private Task<(bool, string, Dictionary<string, object>?)> CheckFileSystemAsync(
        ComponentHealth component,
        CancellationToken cancellationToken)
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new
                {
                    d.Name,
                    FreeGB = d.AvailableFreeSpace / (1024.0 * 1024 * 1024),
                    TotalGB = d.TotalSize / (1024.0 * 1024 * 1024)
                })
                .ToList();

            var metadata = new Dictionary<string, object>
            {
                ["drives"] = drives.Count,
                ["total_free_gb"] = drives.Sum(d => d.FreeGB)
            };

            return Task.FromResult((true, "File system accessible", (Dictionary<string, object>?)metadata));
        }
        catch (Exception ex)
        {
            return Task.FromResult<(bool, string, Dictionary<string, object>?)>((false, $"File system check failed: {ex.Message}", null));
        }
    }

    private async Task<(bool, string, Dictionary<string, object>?)> CheckNetworkAsync(
        ComponentHealth component,
        CancellationToken cancellationToken)
    {
        var target = component.Endpoint ?? "8.8.8.8";
        const int port = 53;

        try
        {
            var sw = Stopwatch.StartNew();
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(target, port, cancellationToken);
            sw.Stop();

            var metadata = new Dictionary<string, object>
            {
                ["target"] = $"{target}:{port}",
                ["latency_ms"] = sw.ElapsedMilliseconds
            };

            return (true, $"Network reachable ({sw.ElapsedMilliseconds} ms)", metadata);
        }
        catch (Exception ex)
        {
            return (false, $"Network check failed for {target}:{port} — {ex.Message}", null);
        }
    }

    private void UpdateComponentHealth(string componentName, ComponentCheckResult result)
    {
        if (!_componentHealth.TryGetValue(componentName, out var currentHealth))
            return;

        var isHealthy = result.Status == ComponentStatus.Healthy;

        if (isHealthy)
        {
            _consecutiveFailures[componentName] = 0;
            _consecutiveSuccesses.AddOrUpdate(componentName, 1, (_, count) => count + 1);
        }
        else
        {
            _consecutiveSuccesses[componentName] = 0;
            _consecutiveFailures.AddOrUpdate(componentName, 1, (_, count) => count + 1);
        }

        var status = DetermineComponentStatus(componentName, isHealthy);

        var updated = currentHealth with
        {
            Status = status,
            LastCheckedAt = result.CheckedAt,
            LastHealthyAt = isHealthy ? result.CheckedAt : currentHealth.LastHealthyAt,
            ConsecutiveFailures = _consecutiveFailures[componentName]
        };

        _componentHealth[componentName] = updated;
    }

    private ComponentStatus DetermineComponentStatus(string componentName, bool isCurrentlyHealthy)
    {
        var failures = _consecutiveFailures.GetValueOrDefault(componentName, 0);
        var successes = _consecutiveSuccesses.GetValueOrDefault(componentName, 0);

        if (failures >= _options.UnhealthyThreshold)
        {
            return ComponentStatus.Unhealthy;
        }

        if (successes >= _options.HealthyThreshold)
        {
            return ComponentStatus.Healthy;
        }

        if (failures > 0)
        {
            return ComponentStatus.Degraded;
        }

        return isCurrentlyHealthy ? ComponentStatus.Healthy : ComponentStatus.Degraded;
    }

    private OverallHealthStatus DetermineOverallStatus(IEnumerable<ComponentCheckResult> results)
    {
        var resultList = results.ToList();

        // Check critical components
        var criticalUnhealthy = _componentHealth.Values
            .Where(c => c.IsCritical && c.Status == ComponentStatus.Unhealthy)
            .Any();

        if (criticalUnhealthy)
        {
            return OverallHealthStatus.Unhealthy;
        }

        var unhealthyCount = resultList.Count(r => r.Status == ComponentStatus.Unhealthy);
        var degradedCount = resultList.Count(r => r.Status == ComponentStatus.Degraded);

        if (unhealthyCount > resultList.Count / 2)
        {
            return OverallHealthStatus.Unhealthy;
        }

        if (unhealthyCount > 0 || degradedCount > 0)
        {
            return OverallHealthStatus.Degraded;
        }

        return OverallHealthStatus.Healthy;
    }

    private async Task SendHealthAlertAsync(HealthCheckResult result, CancellationToken cancellationToken)
    {
        if (_notificationService == null)
            return;

        var unhealthyComponents = result.ComponentResults
            .Where(kvp => kvp.Value.Status == ComponentStatus.Unhealthy)
            .Select(kvp => kvp.Key)
            .ToList();

        var severity = result.OverallStatus == OverallHealthStatus.Unhealthy
            ? AlertSeverity.Critical
            : AlertSeverity.High;

        var message = $"System health check detected issues. " +
                      $"Overall status: {result.OverallStatus}. " +
                      $"Unhealthy components: {string.Join(", ", unhealthyComponents)}";

        await _notificationService.SendAlertAsync(
            severity,
            "Health Check Alert",
            message,
            cancellationToken);
    }

    private static double CalculateUptimePercentage(List<HealthCheckResult> history)
    {
        if (history.Count == 0)
            return 100.0;

        var healthyChecks = history.Count(h => h.OverallStatus == OverallHealthStatus.Healthy);
        return (healthyChecks * 100.0) / history.Count;
    }

    private static double CalculateAverageResponseTime(List<HealthCheckResult> history)
    {
        if (history.Count == 0)
            return 0.0;

        return history
            .SelectMany(h => h.ComponentResults.Values)
            .Where(c => c.ResponseTimeMs >= 0)
            .DefaultIfEmpty(new ComponentCheckResult("", ComponentStatus.Unknown, "", 0, DateTimeOffset.UtcNow, null))
            .Average(c => c.ResponseTimeMs);
    }

    private static int CountStatusChanges(List<HealthCheckResult> history)
    {
        if (history.Count < 2)
            return 0;

        var ordered = history.OrderBy(h => h.CheckedAt).ToList();
        var changes = 0;

        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].OverallStatus != ordered[i - 1].OverallStatus)
            {
                changes++;
            }
        }

        return changes;
    }
}

public interface IHealthCheckMonitor
{
    Task<OverallHealthStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, ComponentHealth>> GetComponentHealthAsync(CancellationToken cancellationToken = default);
    Task<ComponentHealth?> GetComponentHealthAsync(string componentName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthCheckResult>> GetHealthHistoryAsync(int count = 100, CancellationToken cancellationToken = default);
    Task<HealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default);
    Task<string> RegisterComponentAsync(string name, ComponentType type, string? connectionString = null, string? endpoint = null, bool isCritical = false, CancellationToken cancellationToken = default);
    Task<bool> UnregisterComponentAsync(string componentName, CancellationToken cancellationToken = default);
    Task<HealthSummary> GetHealthSummaryAsync(CancellationToken cancellationToken = default);
}

public record ComponentHealth(
    string ComponentName,
    ComponentType Type,
    ComponentStatus Status,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? LastHealthyAt,
    int ConsecutiveFailures,
    bool IsCritical,
    string? ConnectionString,
    string? Endpoint);

public record ComponentCheckResult(
    string ComponentName,
    ComponentStatus Status,
    string Message,
    long ResponseTimeMs,
    DateTimeOffset CheckedAt,
    Dictionary<string, object>? Metadata);

public record HealthCheckResult(
    string CheckId,
    DateTimeOffset CheckedAt,
    TimeSpan Duration,
    OverallHealthStatus OverallStatus,
    Dictionary<string, ComponentCheckResult> ComponentResults,
    int TotalComponents,
    int HealthyComponents,
    int UnhealthyComponents,
    int DegradedComponents);

public record HealthSummary(
    OverallHealthStatus CurrentStatus,
    int TotalComponents,
    int HealthyComponents,
    int UnhealthyComponents,
    int DegradedComponents,
    int CriticalComponentsDown,
    double UptimePercentage24h,
    double AverageResponseTimeMs,
    DateTimeOffset? LastCheckAt,
    int StatusChanges24h);

public enum ComponentType
{
    Database,
    Storage,
    Cache,
    MessageQueue,
    ExternalApi,
    FileSystem,
    Network,
    Custom
}

public enum ComponentStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

public enum OverallHealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}
