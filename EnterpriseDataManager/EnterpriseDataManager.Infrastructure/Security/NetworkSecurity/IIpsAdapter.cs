namespace EnterpriseDataManager.Infrastructure.Security.NetworkSecurity;

public interface IIpsAdapter
{
    Task<IpsStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IpsEvent>> GetRecentEventsAsync(int count = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IpsEvent>> GetEventsBySourceAsync(string sourceIp, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IpsSignature>> GetActiveSignaturesAsync(CancellationToken cancellationToken = default);
    Task EnableSignatureAsync(string signatureId, CancellationToken cancellationToken = default);
    Task DisableSignatureAsync(string signatureId, CancellationToken cancellationToken = default);
    Task BlockIpAsync(string ipAddress, TimeSpan? duration = null, string? reason = null, CancellationToken cancellationToken = default);
    Task UnblockIpAsync(string ipAddress, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetBlockedIpsAsync(CancellationToken cancellationToken = default);
    Task UpdateSignaturesAsync(CancellationToken cancellationToken = default);
}

public record IpsStatus(
    bool IsEnabled,
    bool IsInlineMode,
    IpsMode Mode,
    long PacketsInspected,
    long PacketsBlocked,
    long ActiveConnections,
    DateTimeOffset? LastSignatureUpdate,
    string SignatureVersion);

public record IpsEvent(
    string EventId,
    DateTimeOffset Timestamp,
    string SourceIp,
    int SourcePort,
    string DestinationIp,
    int DestinationPort,
    string Protocol,
    string SignatureId,
    string SignatureName,
    IpsAction Action,
    IpsSeverity Severity,
    string? Payload);

public record IpsSignature(
    string SignatureId,
    string Name,
    string Description,
    string Category,
    IpsSeverity Severity,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public enum IpsMode
{
    Detection,
    Prevention,
    Learning
}

public enum IpsAction
{
    Alert,
    Drop,
    Reset,
    Allow,
    Log
}

public enum IpsSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}
