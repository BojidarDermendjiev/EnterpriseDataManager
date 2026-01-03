namespace EnterpriseDataManager.Infrastructure.Security.RansomwareProtection;

public interface IRansomwareProtector
{
    Task<ProtectionStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task EnableProtectionAsync(CancellationToken cancellationToken = default);
    Task DisableProtectionAsync(CancellationToken cancellationToken = default);
    Task<FileAnalysisResult> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<FileAnalysisResult> AnalyzeStreamAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SuspiciousActivity>> GetRecentActivityAsync(int count = 100, CancellationToken cancellationToken = default);
    Task<bool> IsFileProtectedAsync(string filePath, CancellationToken cancellationToken = default);
    Task ProtectFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task UnprotectFileAsync(string filePath, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetProtectedPathsAsync(CancellationToken cancellationToken = default);
    Task AddProtectedPathAsync(string path, CancellationToken cancellationToken = default);
    Task RemoveProtectedPathAsync(string path, CancellationToken cancellationToken = default);
}

public record ProtectionStatus(
    bool IsEnabled,
    int ProtectedFilesCount,
    int ProtectedPathsCount,
    int BlockedAttemptsLast24Hours,
    DateTimeOffset? LastThreatDetected,
    IReadOnlyList<ActiveThreat> ActiveThreats);

public record FileAnalysisResult(
    string FilePath,
    bool IsSuspicious,
    double RiskScore,
    IReadOnlyList<RiskIndicator> Indicators,
    DateTimeOffset AnalyzedAt,
    string? RecommendedAction);

public record RiskIndicator(
    string Code,
    string Description,
    RiskLevel Level,
    double Weight);

public record SuspiciousActivity(
    string ActivityId,
    DateTimeOffset Timestamp,
    ActivityType Type,
    string SourcePath,
    string? TargetPath,
    string? ProcessName,
    string? UserName,
    RiskLevel RiskLevel,
    bool WasBlocked,
    string Description);

public record ActiveThreat(
    string ThreatId,
    DateTimeOffset DetectedAt,
    string Description,
    RiskLevel Level,
    ThreatState State,
    IReadOnlyList<string> AffectedPaths);

public enum RiskLevel
{
    None,
    Low,
    Medium,
    High,
    Critical
}

public enum ActivityType
{
    MassFileRename,
    MassFileDelete,
    MassFileEncrypt,
    SuspiciousExtensionChange,
    UnauthorizedAccess,
    RapidFileModification,
    KnownRansomwareSignature,
    SuspiciousProcessBehavior
}

public enum ThreatState
{
    Active,
    Contained,
    Remediated,
    Dismissed
}
