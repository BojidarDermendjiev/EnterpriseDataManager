namespace EnterpriseDataManager.Core.Exceptions;

public class StorageException : DomainException
{
    public Guid? StorageProviderId { get; }
    public string? Path { get; }

    public StorageException(string code, string message, Exception? innerException = null)
        : base(code, message, innerException)
    {
    }

    public StorageException(string code, string message, Guid? providerId = null, string? path = null, Exception? innerException = null)
        : base(code, message, innerException)
    {
        StorageProviderId = providerId;
        Path = path;
    }

    public static StorageException ProviderNotFound(Guid providerId)
        => new(DomainErrorCodes.StorageProviderNotFound, $"Storage provider with ID '{providerId}' was not found.", providerId: providerId);

    public static StorageException ProviderDisabled(Guid providerId)
        => new(DomainErrorCodes.StorageProviderDisabled, $"Storage provider with ID '{providerId}' is disabled.", providerId: providerId);

    public static StorageException ConnectionFailed(Guid providerId, string details, Exception? innerException = null)
        => new(DomainErrorCodes.StorageConnectionFailed, $"Failed to connect to storage provider '{providerId}': {details}", providerId: providerId, innerException: innerException);

    public static StorageException QuotaExceeded(Guid providerId, long currentUsage, long quota)
        => new(DomainErrorCodes.StorageQuotaExceeded, $"Storage quota exceeded for provider '{providerId}'. Current usage: {currentUsage} bytes, Quota: {quota} bytes.", providerId: providerId);

    public static StorageException AccessDenied(Guid providerId, string path)
        => new(DomainErrorCodes.StorageAccessDenied, $"Access denied to path '{path}' on storage provider '{providerId}'.", providerId: providerId, path: path);

    public static StorageException ForFileNotFound(Guid providerId, string path)
        => new(DomainErrorCodes.FileNotFound, $"File not found at path '{path}' on storage provider '{providerId}'.", providerId: providerId, path: path);

    public static StorageException ForFileAlreadyExists(Guid providerId, string path)
        => new(DomainErrorCodes.FileAlreadyExists, $"File already exists at path '{path}' on storage provider '{providerId}'.", providerId: providerId, path: path);

    public static StorageException InvalidPath(string path)
        => new(DomainErrorCodes.InvalidStoragePath, $"Invalid storage path: '{path}'.", path: path);

    public static StorageException OperationFailed(Guid providerId, string operation, string details, Exception? innerException = null)
        => new(DomainErrorCodes.StorageConnectionFailed, $"Storage operation '{operation}' failed on provider '{providerId}': {details}", providerId: providerId, innerException: innerException);
}
