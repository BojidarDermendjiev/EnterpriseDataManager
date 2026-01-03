namespace EnterpriseDataManager.Infrastructure.Security.NetworkSecurity;

public interface IEdrAdapter
{
    Task<EdrScanResult> ScanFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<EdrScanResult> ScanStreamAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EdrThreat>> GetActiveThreatsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EdrAlert>> GetRecentAlertsAsync(int count = 100, CancellationToken cancellationToken = default);
    Task QuarantineFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<bool> RestoreFromQuarantineAsync(string quarantineId, CancellationToken cancellationToken = default);
    Task<EdrAgentStatus> GetAgentStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateDefinitionsAsync(CancellationToken cancellationToken = default);
}

public record EdrScanResult(
    bool IsClean,
    string FilePath,
    IReadOnlyList<EdrThreat> ThreatsFound,
    DateTimeOffset ScannedAt,
    TimeSpan ScanDuration);

public record EdrThreat(
    string ThreatId,
    string ThreatName,
    ThreatSeverity Severity,
    ThreatType Type,
    string? FilePath,
    string? ProcessName,
    DateTimeOffset DetectedAt,
    ThreatStatus Status,
    string? Description);

public record EdrAlert(
    string AlertId,
    string Title,
    string Description,
    ThreatSeverity Severity,
    DateTimeOffset CreatedAt,
    bool IsAcknowledged,
    string? AssignedTo,
    IReadOnlyList<string> RelatedThreatIds);

public record EdrAgentStatus(
    bool IsRunning,
    bool IsConnected,
    DateTimeOffset? LastDefinitionUpdate,
    DateTimeOffset? LastFullScan,
    string AgentVersion,
    string DefinitionVersion,
    int PendingThreats);

public enum ThreatSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum ThreatType
{
    Malware,
    Ransomware,
    Trojan,
    Worm,
    Spyware,
    Adware,
    Rootkit,
    Exploit,
    PotentiallyUnwanted,
    Suspicious
}

public enum ThreatStatus
{
    Active,
    Quarantined,
    Removed,
    Allowed,
    Remediated
}
