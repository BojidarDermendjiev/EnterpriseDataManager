namespace EnterpriseDataManager.Application.DTOs;

using EnterpriseDataManager.Core.Enums;

public sealed record StorageProviderDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public StorageType Type { get; init; }
    public string? Endpoint { get; init; }
    public string? BucketOrContainer { get; init; }
    public string? RootPath { get; init; }
    public bool HasCredentials { get; init; }
    public bool IsImmutable { get; init; }
    public bool IsEnabled { get; init; }
    public long? QuotaBytes { get; init; }
    public int ArchivePlanCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

public sealed record StorageProviderSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public StorageType Type { get; init; }
    public bool IsEnabled { get; init; }
    public long? QuotaBytes { get; init; }
}

public sealed record CreateLocalStorageProviderDto
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string RootPath { get; init; } = default!;
    public long? QuotaBytes { get; init; }
}

public sealed record CreateS3StorageProviderDto
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string Endpoint { get; init; } = default!;
    public string Bucket { get; init; } = default!;
    public string? CredentialsReference { get; init; }
    public string? RootPath { get; init; }
    public long? QuotaBytes { get; init; }
}

public sealed record CreateAzureBlobStorageProviderDto
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string Endpoint { get; init; } = default!;
    public string Container { get; init; } = default!;
    public string? CredentialsReference { get; init; }
    public string? RootPath { get; init; }
    public long? QuotaBytes { get; init; }
}

public sealed record UpdateStorageProviderDto
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string? Endpoint { get; init; }
    public string? BucketOrContainer { get; init; }
    public string? RootPath { get; init; }
    public string? CredentialsReference { get; init; }
    public long? QuotaBytes { get; init; }
}

public sealed record StorageHealthDto
{
    public Guid ProviderId { get; init; }
    public string ProviderName { get; init; } = default!;
    public bool IsHealthy { get; init; }
    public bool IsReachable { get; init; }
    public TimeSpan? Latency { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
}

public sealed record StorageUsageDto
{
    public Guid ProviderId { get; init; }
    public string ProviderName { get; init; } = default!;
    public long UsedBytes { get; init; }
    public long? QuotaBytes { get; init; }
    public double? UsagePercentage { get; init; }
    public int FileCount { get; init; }
    public DateTimeOffset CalculatedAt { get; init; }
}
