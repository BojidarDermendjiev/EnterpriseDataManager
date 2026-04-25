namespace EnterpriseDataManager.Infrastructure.Storage;

using Amazon.Runtime;
using Amazon.S3;
using Azure.Storage.Blobs;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;

public interface IStorageProviderFactory
{
    IStorageProviderAdapter CreateForProvider(StorageProvider provider);
}

public class StorageProviderFactory : IStorageProviderFactory
{
    public IStorageProviderAdapter CreateForProvider(StorageProvider provider)
    {
        return provider.Type switch
        {
            StorageType.Local => new LocalFilesystemProvider(
                provider.RootPath ?? throw new InvalidOperationException(
                    $"StorageProvider '{provider.Name}' is missing RootPath")),
            StorageType.S3Compatible => CreateS3Provider(provider),
            StorageType.AzureBlob => CreateAzureBlobProvider(provider),
            _ => throw new NotSupportedException($"Storage type '{provider.Type}' is not supported")
        };
    }

    private static IStorageProviderAdapter CreateS3Provider(StorageProvider provider)
    {
        var endpoint = provider.Endpoint
            ?? throw new InvalidOperationException($"StorageProvider '{provider.Name}' is missing Endpoint");
        var bucket = provider.BucketOrContainer
            ?? throw new InvalidOperationException($"StorageProvider '{provider.Name}' is missing BucketOrContainer");

        var credentialsParts = provider.CredentialsReference?.Split(':', 2);
        var accessKey = credentialsParts?.Length == 2 ? credentialsParts[0] : string.Empty;
        var secretKey = credentialsParts?.Length == 2 ? credentialsParts[1] : string.Empty;

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var s3Config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true
        };
        var s3Client = new AmazonS3Client(credentials, s3Config);

        var prefix = provider.RootPath;
        return new S3CompatibleProvider(accessKey, s3Client, bucket, prefix);
    }

    private static IStorageProviderAdapter CreateAzureBlobProvider(StorageProvider provider)
    {
        var connectionString = provider.CredentialsReference
            ?? throw new InvalidOperationException($"StorageProvider '{provider.Name}' is missing CredentialsReference (connection string)");
        var container = provider.BucketOrContainer
            ?? throw new InvalidOperationException($"StorageProvider '{provider.Name}' is missing BucketOrContainer");

        var containerClient = new BlobContainerClient(connectionString, container);
        var prefix = provider.RootPath;
        return new AzureBlobProvider(containerClient, prefix);
    }
}
