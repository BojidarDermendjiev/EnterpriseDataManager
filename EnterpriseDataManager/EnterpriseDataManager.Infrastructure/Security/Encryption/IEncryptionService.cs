namespace EnterpriseDataManager.Infrastructure.Security.Encryption;

public interface IEncryptionService
{
    Task<EncryptedData> EncryptAsync(Stream plaintext, CancellationToken cancellationToken = default);
    Task<EncryptedData> EncryptAsync(byte[] plaintext, CancellationToken cancellationToken = default);
    Task<Stream> DecryptAsync(EncryptedData encryptedData, CancellationToken cancellationToken = default);
    Task<byte[]> DecryptToBytesAsync(EncryptedData encryptedData, CancellationToken cancellationToken = default);
    Task<string> EncryptStringAsync(string plaintext, CancellationToken cancellationToken = default);
    Task<string> DecryptStringAsync(string encryptedBase64, CancellationToken cancellationToken = default);
    byte[] DeriveKey(string password, byte[] salt, int keyLength = 32);
    Task<byte[]> GenerateKeyAsync(int keyLength = 32, CancellationToken cancellationToken = default);
    byte[] GenerateSalt(int length = 16);
}

public record EncryptedData(
    byte[] Ciphertext,
    byte[] IV,
    byte[] Salt,
    string Algorithm,
    int KeyDerivationIterations);
