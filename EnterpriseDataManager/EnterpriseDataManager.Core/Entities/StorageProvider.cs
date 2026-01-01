using EnterpriseDataManager.Common;
using EnterpriseDataManager.Core.Entities.Common;
using EnterpriseDataManager.Core.Enums;
using static EnterpriseDataManager.Common.EntityValidationConstants;

namespace EnterpriseDataManager.Core.Entities;

public class StorageProvider : BaseEntity
{
    private readonly List<ArchivePlan> _archivePlans = new();

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public StorageType Type { get; private set; }
    public string? Endpoint { get; private set; }
    public string? BucketOrContainer { get; private set; }
    public string? RootPath { get; private set; }
    public string? CredentialsReference { get; private set; }
    public bool IsImmutable { get; private set; }
    public bool IsEnabled { get; private set; }
    public long? QuotaBytes { get; private set; }
    public IReadOnlyCollection<ArchivePlan> ArchivePlans => _archivePlans.AsReadOnly();

    protected StorageProvider() { }

    private StorageProvider(string name, StorageType type)
    {
        Name = name;
        Type = type;
        IsEnabled = true;
    }

    public static StorageProvider Create(string name, StorageType type)
    {
        Guard.AgainstNullOrWhiteSpace(name, NameCannotBeEmpty);

        return new StorageProvider(name.Trim(), type);
    }

    public static StorageProvider CreateLocal(string name, string rootPath)
    {
        var provider = Create(name, StorageType.Local);
        provider.RootPath = rootPath;
        return provider;
    }

    public static StorageProvider CreateS3(string name, string endpoint, string bucket, string? credentialsRef = null)
    {
        var provider = Create(name, StorageType.S3Compatible);
        provider.Endpoint = endpoint;
        provider.BucketOrContainer = bucket;
        provider.CredentialsReference = credentialsRef;
        return provider;
    }

    public static StorageProvider CreateAzureBlob(string name, string endpoint, string container, string? credentialsRef = null)
    {
        var provider = Create(name, StorageType.AzureBlob);
        provider.Endpoint = endpoint;
        provider.BucketOrContainer = container;
        provider.CredentialsReference = credentialsRef;
        return provider;
    }

    public void UpdateDetails(string name, string? description = null)
    {
        Guard.AgainstNullOrWhiteSpace(name, NameCannotBeEmpty);

        Name = name.Trim();
        Description = description?.Trim();
    }

    public void SetEndpoint(string endpoint)
    {
        Guard.AgainstInvalidOperation(Type == StorageType.Local, LocalStorageDoesNotUseEndpoints);
        Endpoint = endpoint?.Trim();
    }

    public void SetBucketOrContainer(string bucketOrContainer)
    {
        Guard.AgainstInvalidOperation(Type == StorageType.Local, LocalStorageDoesNotUseBuckets);
        BucketOrContainer = bucketOrContainer?.Trim();
    }

    public void SetRootPath(string? rootPath) => RootPath = rootPath?.Trim();

    public void SetCredentialsReference(string? reference) => CredentialsReference = reference?.Trim();

    public void SetQuota(long? quotaBytes)
    {
        if (quotaBytes.HasValue)
            Guard.AgainstNegative(quotaBytes.Value, QuotaCannotBeNegative);

        QuotaBytes = quotaBytes;
    }

    public void MakeImmutable() => IsImmutable = true;

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public string GetFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(RootPath))
            return relativePath;

        return Path.Combine(RootPath, relativePath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
