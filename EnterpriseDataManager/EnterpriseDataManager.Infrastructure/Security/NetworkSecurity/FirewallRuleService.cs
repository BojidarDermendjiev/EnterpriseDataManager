namespace EnterpriseDataManager.Infrastructure.Security.NetworkSecurity;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

public class FirewallRuleService : IFirewallManager
{
    private readonly ConcurrentDictionary<Guid, FirewallRule> _rules = new();
    private readonly ILogger<FirewallRuleService>? _logger;

    public FirewallRuleService(ILogger<FirewallRuleService>? logger = null)
    {
        _logger = logger;
    }

    public Task<FirewallRule> CreateRuleAsync(FirewallRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = new FirewallRule(
            Id: Guid.NewGuid(),
            Name: request.Name,
            Description: request.Description,
            Action: request.Action,
            Direction: request.Direction,
            Protocol: request.Protocol,
            SourceAddress: request.SourceAddress,
            SourcePort: request.SourcePort,
            DestinationAddress: request.DestinationAddress,
            DestinationPort: request.DestinationPort,
            Priority: request.Priority,
            IsEnabled: request.IsEnabled,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null);

        _rules[rule.Id] = rule;
        _logger?.LogInformation("Created firewall rule {RuleId}: {RuleName}", rule.Id, rule.Name);

        return Task.FromResult(rule);
    }

    public Task<FirewallRule> UpdateRuleAsync(Guid ruleId, FirewallRuleRequest request, CancellationToken cancellationToken = default)
    {
        if (!_rules.TryGetValue(ruleId, out var existing))
        {
            throw new KeyNotFoundException($"Firewall rule {ruleId} not found");
        }

        var updated = existing with
        {
            Name = request.Name,
            Description = request.Description,
            Action = request.Action,
            Direction = request.Direction,
            Protocol = request.Protocol,
            SourceAddress = request.SourceAddress,
            SourcePort = request.SourcePort,
            DestinationAddress = request.DestinationAddress,
            DestinationPort = request.DestinationPort,
            Priority = request.Priority,
            IsEnabled = request.IsEnabled,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _rules[ruleId] = updated;
        _logger?.LogInformation("Updated firewall rule {RuleId}: {RuleName}", ruleId, updated.Name);

        return Task.FromResult(updated);
    }

    public Task DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        if (_rules.TryRemove(ruleId, out var rule))
        {
            _logger?.LogInformation("Deleted firewall rule {RuleId}: {RuleName}", ruleId, rule.Name);
        }

        return Task.CompletedTask;
    }

    public Task<FirewallRule?> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        _rules.TryGetValue(ruleId, out var rule);
        return Task.FromResult(rule);
    }

    public Task<IReadOnlyList<FirewallRule>> GetAllRulesAsync(CancellationToken cancellationToken = default)
    {
        var rules = _rules.Values
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<FirewallRule>>(rules);
    }

    public Task<IReadOnlyList<FirewallRule>> GetRulesByPriorityAsync(int minPriority, int maxPriority, CancellationToken cancellationToken = default)
    {
        var rules = _rules.Values
            .Where(r => r.Priority >= minPriority && r.Priority <= maxPriority)
            .OrderBy(r => r.Priority)
            .ToList();

        return Task.FromResult<IReadOnlyList<FirewallRule>>(rules);
    }

    public Task EnableRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        if (_rules.TryGetValue(ruleId, out var rule))
        {
            _rules[ruleId] = rule with { IsEnabled = true, UpdatedAt = DateTimeOffset.UtcNow };
            _logger?.LogInformation("Enabled firewall rule {RuleId}", ruleId);
        }

        return Task.CompletedTask;
    }

    public Task DisableRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        if (_rules.TryGetValue(ruleId, out var rule))
        {
            _rules[ruleId] = rule with { IsEnabled = false, UpdatedAt = DateTimeOffset.UtcNow };
            _logger?.LogInformation("Disabled firewall rule {RuleId}", ruleId);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> TestConnectionAsync(
        string sourceIp,
        string destinationIp,
        int port,
        FirewallProtocol protocol,
        CancellationToken cancellationToken = default)
    {
        var applicableRules = _rules.Values
            .Where(r => r.IsEnabled)
            .Where(r => MatchesAddress(r.SourceAddress, sourceIp))
            .Where(r => MatchesAddress(r.DestinationAddress, destinationIp))
            .Where(r => MatchesPort(r.DestinationPort, port))
            .Where(r => r.Protocol == FirewallProtocol.Any || r.Protocol == protocol)
            .OrderBy(r => r.Priority)
            .ToList();

        foreach (var rule in applicableRules)
        {
            return rule.Action == FirewallAction.Allow;
        }

        // Default allow if no rules match
        return await Task.FromResult(true);
    }

    public Task ReloadRulesAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Firewall rules reloaded. Total rules: {Count}", _rules.Count);
        return Task.CompletedTask;
    }

    private static bool MatchesAddress(string? ruleAddress, string testAddress)
    {
        if (string.IsNullOrEmpty(ruleAddress) || ruleAddress == "*")
            return true;

        if (ruleAddress.Contains('/'))
        {
            return IsInSubnet(testAddress, ruleAddress);
        }

        return ruleAddress.Equals(testAddress, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesPort(string? rulePort, int testPort)
    {
        if (string.IsNullOrEmpty(rulePort) || rulePort == "*")
            return true;

        if (rulePort.Contains('-'))
        {
            var parts = rulePort.Split('-');
            if (int.TryParse(parts[0], out var min) && int.TryParse(parts[1], out var max))
            {
                return testPort >= min && testPort <= max;
            }
        }

        return int.TryParse(rulePort, out var port) && port == testPort;
    }

    private static bool IsInSubnet(string ipAddress, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            var networkAddress = IPAddress.Parse(parts[0]);
            var prefixLength = int.Parse(parts[1]);
            var testAddress = IPAddress.Parse(ipAddress);

            var networkBytes = networkAddress.GetAddressBytes();
            var testBytes = testAddress.GetAddressBytes();

            if (networkBytes.Length != testBytes.Length)
                return false;

            var bytesToCheck = prefixLength / 8;
            var bitsToCheck = prefixLength % 8;

            for (var i = 0; i < bytesToCheck; i++)
            {
                if (networkBytes[i] != testBytes[i])
                    return false;
            }

            if (bitsToCheck > 0 && bytesToCheck < networkBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - bitsToCheck));
                if ((networkBytes[bytesToCheck] & mask) != (testBytes[bytesToCheck] & mask))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
