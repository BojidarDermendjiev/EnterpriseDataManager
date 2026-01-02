namespace EnterpriseDataManager.Application.Commands.StorageProviders;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Application.DTOs;

public sealed record CreateLocalStorageProviderCommand(
    string Name,
    string RootPath,
    string? Description = null,
    long? QuotaBytes = null) : ICommand<StorageProviderDto>;

public sealed record CreateS3StorageProviderCommand(
    string Name,
    string Endpoint,
    string Bucket,
    string? Description = null,
    string? CredentialsReference = null,
    string? RootPath = null,
    long? QuotaBytes = null) : ICommand<StorageProviderDto>;

public sealed record CreateAzureBlobStorageProviderCommand(
    string Name,
    string Endpoint,
    string Container,
    string? Description = null,
    string? CredentialsReference = null,
    string? RootPath = null,
    long? QuotaBytes = null) : ICommand<StorageProviderDto>;

public sealed record UpdateStorageProviderCommand(
    Guid Id,
    string Name,
    string? Description = null,
    string? Endpoint = null,
    string? BucketOrContainer = null,
    string? RootPath = null,
    string? CredentialsReference = null,
    long? QuotaBytes = null) : ICommand<StorageProviderDto>;

public sealed record DeleteStorageProviderCommand(Guid Id) : ICommand;

public sealed record EnableStorageProviderCommand(Guid Id) : ICommand<StorageProviderDto>;

public sealed record DisableStorageProviderCommand(Guid Id) : ICommand<StorageProviderDto>;

public sealed record SetStorageQuotaCommand(
    Guid Id,
    long? QuotaBytes) : ICommand<StorageProviderDto>;

public sealed record MakeStorageImmutableCommand(Guid Id) : ICommand<StorageProviderDto>;
