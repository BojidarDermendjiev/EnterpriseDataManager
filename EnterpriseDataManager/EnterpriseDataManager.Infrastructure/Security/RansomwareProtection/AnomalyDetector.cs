namespace EnterpriseDataManager.Infrastructure.Security.RansomwareProtection;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

public class AnomalyDetector
{
    private readonly ILogger<AnomalyDetector>? _logger;
    private readonly AnomalyDetectorOptions _options;
    private readonly ConcurrentQueue<FileActivityEvent> _recentActivity = new();
    private readonly ConcurrentDictionary<string, UserActivityProfile> _userProfiles = new();
    private readonly List<DetectedAnomaly> _detectedAnomalies = new();
    private readonly object _anomalyLock = new();

    public AnomalyDetector(AnomalyDetectorOptions? options = null, ILogger<AnomalyDetector>? logger = null)
    {
        _options = options ?? new AnomalyDetectorOptions();
        _logger = logger;
    }

    public Task RecordActivityAsync(FileActivityEvent activity, CancellationToken cancellationToken = default)
    {
        _recentActivity.Enqueue(activity);

        // Keep only recent activity within the window
        var threshold = DateTimeOffset.UtcNow.Subtract(_options.ActivityWindow);
        while (_recentActivity.TryPeek(out var oldest) && oldest.Timestamp < threshold)
        {
            _recentActivity.TryDequeue(out _);
        }

        // Update user profile
        UpdateUserProfile(activity);

        return Task.CompletedTask;
    }

    public Task<AnomalyAnalysisResult> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var anomalies = new List<DetectedAnomaly>();
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.Subtract(_options.ActivityWindow);

        var recentEvents = _recentActivity.Where(e => e.Timestamp >= windowStart).ToList();

        // Check for mass file operations
        var massOperationAnomaly = DetectMassFileOperations(recentEvents);
        if (massOperationAnomaly != null)
            anomalies.Add(massOperationAnomaly);

        // Check for suspicious extension changes
        var extensionAnomaly = DetectSuspiciousExtensionChanges(recentEvents);
        if (extensionAnomaly != null)
            anomalies.Add(extensionAnomaly);

        // Check for unusual user behavior
        var behaviorAnomalies = DetectUnusualUserBehavior(recentEvents);
        anomalies.AddRange(behaviorAnomalies);

        // Check for known ransomware patterns
        var ransomwareAnomaly = DetectRansomwarePatterns(recentEvents);
        if (ransomwareAnomaly != null)
            anomalies.Add(ransomwareAnomaly);

        // Store detected anomalies
        lock (_anomalyLock)
        {
            _detectedAnomalies.AddRange(anomalies);
        }

        foreach (var anomaly in anomalies)
        {
            _logger?.LogWarning("Detected anomaly: {Type} - {Description} (Risk: {RiskLevel})",
                anomaly.Type, anomaly.Description, anomaly.RiskLevel);
        }

        var riskScore = CalculateOverallRiskScore(anomalies);

        return Task.FromResult(new AnomalyAnalysisResult(
            AnalyzedAt: now,
            EventsAnalyzed: recentEvents.Count,
            AnomaliesDetected: anomalies,
            OverallRiskScore: riskScore,
            RecommendedAction: GetRecommendedAction(riskScore)));
    }

    public Task<IReadOnlyList<DetectedAnomaly>> GetRecentAnomaliesAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        List<DetectedAnomaly> result;
        lock (_anomalyLock)
        {
            result = _detectedAnomalies
                .OrderByDescending(a => a.DetectedAt)
                .Take(count)
                .ToList();
        }

        return Task.FromResult<IReadOnlyList<DetectedAnomaly>>(result);
    }

    public Task<UserActivityProfile?> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        _userProfiles.TryGetValue(userId, out var profile);
        return Task.FromResult(profile);
    }

    public Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        while (_recentActivity.TryDequeue(out _)) { }

        lock (_anomalyLock)
        {
            _detectedAnomalies.Clear();
        }

        _logger?.LogInformation("Anomaly detector history cleared");

        return Task.CompletedTask;
    }

    private void UpdateUserProfile(FileActivityEvent activity)
    {
        _userProfiles.AddOrUpdate(
            activity.UserId,
            key => new UserActivityProfile(
                UserId: key,
                TotalOperations: 1,
                AverageOperationsPerHour: 1,
                LastActivityAt: activity.Timestamp,
                MostAccessedPaths: new List<string> { activity.Path },
                TypicalWorkingHours: new HashSet<int> { activity.Timestamp.Hour }),
            (key, existing) =>
            {
                var paths = existing.MostAccessedPaths.ToList();
                if (!paths.Contains(activity.Path))
                    paths.Add(activity.Path);

                var hours = existing.TypicalWorkingHours.ToHashSet();
                hours.Add(activity.Timestamp.Hour);

                return existing with
                {
                    TotalOperations = existing.TotalOperations + 1,
                    LastActivityAt = activity.Timestamp,
                    MostAccessedPaths = paths.TakeLast(100).ToList(),
                    TypicalWorkingHours = hours
                };
            });
    }

    private DetectedAnomaly? DetectMassFileOperations(List<FileActivityEvent> events)
    {
        var operationCounts = events
            .GroupBy(e => e.OperationType)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (opType, count) in operationCounts)
        {
            var threshold = opType switch
            {
                FileOperationType.Delete => _options.MassDeleteThreshold,
                FileOperationType.Rename => _options.MassRenameThreshold,
                FileOperationType.Modify => _options.MassModifyThreshold,
                _ => int.MaxValue
            };

            if (count >= threshold)
            {
                return new DetectedAnomaly(
                    AnomalyId: Guid.NewGuid().ToString(),
                    Type: AnomalyType.MassOperation,
                    Description: $"Detected {count} {opType} operations in {_options.ActivityWindow.TotalMinutes} minutes",
                    RiskLevel: count >= threshold * 2 ? RiskLevel.Critical : RiskLevel.High,
                    DetectedAt: DateTimeOffset.UtcNow,
                    AffectedPaths: events.Where(e => e.OperationType == opType).Select(e => e.Path).Distinct().ToList(),
                    RelatedUserIds: events.Where(e => e.OperationType == opType).Select(e => e.UserId).Distinct().ToList());
            }
        }

        return null;
    }

    private DetectedAnomaly? DetectSuspiciousExtensionChanges(List<FileActivityEvent> events)
    {
        var suspiciousExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".encrypted", ".locked", ".crypto", ".crypt", ".enc",
            ".locky", ".cerber", ".zepto", ".thor", ".zzz",
            ".micro", ".aaa", ".abc", ".xyz", ".zzzzz"
        };

        var suspiciousRenames = events
            .Where(e => e.OperationType == FileOperationType.Rename)
            .Where(e => !string.IsNullOrEmpty(e.NewPath))
            .Where(e => suspiciousExtensions.Any(ext => e.NewPath!.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (suspiciousRenames.Count >= _options.SuspiciousExtensionThreshold)
        {
            return new DetectedAnomaly(
                AnomalyId: Guid.NewGuid().ToString(),
                Type: AnomalyType.SuspiciousExtensionChange,
                Description: $"Detected {suspiciousRenames.Count} files renamed to suspicious extensions",
                RiskLevel: RiskLevel.Critical,
                DetectedAt: DateTimeOffset.UtcNow,
                AffectedPaths: suspiciousRenames.Select(e => e.Path).Distinct().ToList(),
                RelatedUserIds: suspiciousRenames.Select(e => e.UserId).Distinct().ToList());
        }

        return null;
    }

    private List<DetectedAnomaly> DetectUnusualUserBehavior(List<FileActivityEvent> events)
    {
        var anomalies = new List<DetectedAnomaly>();

        var userEvents = events.GroupBy(e => e.UserId);

        foreach (var group in userEvents)
        {
            var userId = group.Key;
            var userEventList = group.ToList();

            if (_userProfiles.TryGetValue(userId, out var profile))
            {
                // Check for unusual hours
                var currentHour = DateTimeOffset.UtcNow.Hour;
                if (profile.TypicalWorkingHours.Count > 0 && !profile.TypicalWorkingHours.Contains(currentHour))
                {
                    if (userEventList.Count >= _options.UnusualHoursThreshold)
                    {
                        anomalies.Add(new DetectedAnomaly(
                            AnomalyId: Guid.NewGuid().ToString(),
                            Type: AnomalyType.UnusualUserBehavior,
                            Description: $"User {userId} performing {userEventList.Count} operations outside typical hours",
                            RiskLevel: RiskLevel.Medium,
                            DetectedAt: DateTimeOffset.UtcNow,
                            AffectedPaths: userEventList.Select(e => e.Path).Distinct().ToList(),
                            RelatedUserIds: new List<string> { userId }));
                    }
                }
            }
        }

        return anomalies;
    }

    private DetectedAnomaly? DetectRansomwarePatterns(List<FileActivityEvent> events)
    {
        // Check for the classic ransomware pattern: read -> encrypt -> delete original
        var readEvents = events.Where(e => e.OperationType == FileOperationType.Read).Select(e => e.Path).ToHashSet();
        var modifyEvents = events.Where(e => e.OperationType == FileOperationType.Modify).Select(e => e.Path).ToHashSet();
        var deleteEvents = events.Where(e => e.OperationType == FileOperationType.Delete).Select(e => e.Path).ToHashSet();

        var suspiciousPatterns = readEvents.Intersect(modifyEvents).Intersect(deleteEvents).ToList();

        if (suspiciousPatterns.Count >= _options.RansomwarePatternThreshold)
        {
            return new DetectedAnomaly(
                AnomalyId: Guid.NewGuid().ToString(),
                Type: AnomalyType.RansomwarePattern,
                Description: $"Detected ransomware-like pattern affecting {suspiciousPatterns.Count} files",
                RiskLevel: RiskLevel.Critical,
                DetectedAt: DateTimeOffset.UtcNow,
                AffectedPaths: suspiciousPatterns,
                RelatedUserIds: events.Select(e => e.UserId).Distinct().ToList());
        }

        return null;
    }

    private static double CalculateOverallRiskScore(List<DetectedAnomaly> anomalies)
    {
        if (anomalies.Count == 0)
            return 0.0;

        var maxScore = anomalies.Max(a => a.RiskLevel switch
        {
            RiskLevel.Critical => 1.0,
            RiskLevel.High => 0.75,
            RiskLevel.Medium => 0.5,
            RiskLevel.Low => 0.25,
            _ => 0.0
        });

        var avgScore = anomalies.Average(a => a.RiskLevel switch
        {
            RiskLevel.Critical => 1.0,
            RiskLevel.High => 0.75,
            RiskLevel.Medium => 0.5,
            RiskLevel.Low => 0.25,
            _ => 0.0
        });

        return (maxScore * 0.7) + (avgScore * 0.3);
    }

    private static string GetRecommendedAction(double riskScore)
    {
        return riskScore switch
        {
            >= 0.9 => "IMMEDIATE: Isolate affected systems and initiate incident response",
            >= 0.7 => "URGENT: Block suspicious processes and investigate immediately",
            >= 0.5 => "HIGH: Review activity and prepare incident response",
            >= 0.25 => "MODERATE: Monitor closely and gather additional information",
            _ => "LOW: Continue monitoring"
        };
    }
}

public class AnomalyDetectorOptions
{
    public TimeSpan ActivityWindow { get; set; } = TimeSpan.FromMinutes(5);
    public int MassDeleteThreshold { get; set; } = 50;
    public int MassRenameThreshold { get; set; } = 50;
    public int MassModifyThreshold { get; set; } = 100;
    public int SuspiciousExtensionThreshold { get; set; } = 5;
    public int UnusualHoursThreshold { get; set; } = 10;
    public int RansomwarePatternThreshold { get; set; } = 10;
}

public record FileActivityEvent(
    string EventId,
    DateTimeOffset Timestamp,
    string UserId,
    string Path,
    string? NewPath,
    FileOperationType OperationType,
    long? FileSizeBytes);

public record AnomalyAnalysisResult(
    DateTimeOffset AnalyzedAt,
    int EventsAnalyzed,
    IReadOnlyList<DetectedAnomaly> AnomaliesDetected,
    double OverallRiskScore,
    string RecommendedAction);

public record DetectedAnomaly(
    string AnomalyId,
    AnomalyType Type,
    string Description,
    RiskLevel RiskLevel,
    DateTimeOffset DetectedAt,
    IReadOnlyList<string> AffectedPaths,
    IReadOnlyList<string> RelatedUserIds);

public record UserActivityProfile(
    string UserId,
    long TotalOperations,
    double AverageOperationsPerHour,
    DateTimeOffset LastActivityAt,
    IReadOnlyList<string> MostAccessedPaths,
    IReadOnlySet<int> TypicalWorkingHours);

public enum AnomalyType
{
    MassOperation,
    SuspiciousExtensionChange,
    UnusualUserBehavior,
    RansomwarePattern,
    UnauthorizedAccess,
    DataExfiltration
}

public enum FileOperationType
{
    Create,
    Read,
    Modify,
    Delete,
    Rename,
    Move,
    Copy
}
