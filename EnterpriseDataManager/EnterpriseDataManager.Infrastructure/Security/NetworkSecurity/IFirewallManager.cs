namespace EnterpriseDataManager.Infrastructure.Security.NetworkSecurity;

public interface IFirewallManager
{
    Task<FirewallRule> CreateRuleAsync(FirewallRuleRequest request, CancellationToken cancellationToken = default);
    Task<FirewallRule> UpdateRuleAsync(Guid ruleId, FirewallRuleRequest request, CancellationToken cancellationToken = default);
    Task DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<FirewallRule?> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FirewallRule>> GetAllRulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FirewallRule>> GetRulesByPriorityAsync(int minPriority, int maxPriority, CancellationToken cancellationToken = default);
    Task EnableRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task DisableRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(string sourceIp, string destinationIp, int port, FirewallProtocol protocol, CancellationToken cancellationToken = default);
    Task ReloadRulesAsync(CancellationToken cancellationToken = default);
}

public record FirewallRule(
    Guid Id,
    string Name,
    string? Description,
    FirewallAction Action,
    FirewallDirection Direction,
    FirewallProtocol Protocol,
    string? SourceAddress,
    string? SourcePort,
    string? DestinationAddress,
    string? DestinationPort,
    int Priority,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record FirewallRuleRequest(
    string Name,
    string? Description,
    FirewallAction Action,
    FirewallDirection Direction,
    FirewallProtocol Protocol,
    string? SourceAddress,
    string? SourcePort,
    string? DestinationAddress,
    string? DestinationPort,
    int Priority,
    bool IsEnabled = true);

public enum FirewallAction
{
    Allow,
    Deny,
    Drop,
    Reject,
    Log
}

public enum FirewallDirection
{
    Inbound,
    Outbound,
    Both
}

public enum FirewallProtocol
{
    Any,
    Tcp,
    Udp,
    Icmp,
    IcmpV6
}
