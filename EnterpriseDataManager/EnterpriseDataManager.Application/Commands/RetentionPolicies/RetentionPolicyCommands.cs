namespace EnterpriseDataManager.Application.Commands.RetentionPolicies;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;

public sealed record CreateRetentionPolicyCommand(
    string Name,
    int RetentionDays,
    string? Description = null,
    string? Scope = null) : ICommand<RetentionPolicyDto>;

public sealed record UpdateRetentionPolicyCommand(
    Guid Id,
    string Name,
    string? Description = null,
    int? RetentionDays = null,
    string? Scope = null) : ICommand<RetentionPolicyDto>;

public sealed record DeleteRetentionPolicyCommand(Guid Id) : ICommand;

public sealed record EnableLegalHoldCommand(
    Guid Id,
    string? Reason = null) : ICommand<RetentionPolicyDto>;

public sealed record DisableLegalHoldCommand(
    Guid Id,
    string? Reason = null) : ICommand<RetentionPolicyDto>;

public sealed record MakeRetentionPolicyImmutableCommand(Guid Id) : ICommand<RetentionPolicyDto>;

public sealed record ProcessRetentionPoliciesCommand : ICommand;
