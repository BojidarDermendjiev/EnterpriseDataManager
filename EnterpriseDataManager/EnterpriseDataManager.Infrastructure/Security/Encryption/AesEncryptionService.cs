namespace EnterpriseDataManager.Infrastructure.Security.Encryption;

using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

public class EncryptionOptions
{
    public const string SectionName = "Encryption";

    public string MasterKey { get; set; } = string.Empty;
    public int KeyDerivationIterations { get; set; } = 100000;
    public int KeySize { get; set; } = 256;
    public int SaltSize { get; set; } = 16;
}

public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _masterKey;
    private readonly int _keyDerivationIterations;
    private readonly int _saltSize;
    private const string Algorithm = "AES-256-GCM";

    public AesEncryptionService(IOptions<EncryptionOptions> options)
    {
        var config = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrEmpty(config.MasterKey))
        {
            throw new InvalidOperationException("Encryption master key is not configured");
        }

        _masterKey = Convert.FromBase64String(config.MasterKey);
        _keyDerivationIterations = config.KeyDerivationIterations;
        _saltSize = config.SaltSize;

        if (_masterKey.Length < 32)
        {
            throw new InvalidOperationException("Master key must be at least 256 bits (32 bytes)");
        }
    }

    public AesEncryptionService(byte[] masterKey, int keyDerivationIterations = 100000, int saltSize = 16)
    {
        _masterKey = masterKey ?? throw new ArgumentNullException(nameof(masterKey));
        _keyDerivationIterations = keyDerivationIterations;
        _saltSize = saltSize;

        if (_masterKey.Length < 32)
        {
            throw new InvalidOperationException("Master key must be at least 256 bits (32 bytes)");
        }
    }

    public async Task<EncryptedData> EncryptAsync(Stream plaintext, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await plaintext.CopyToAsync(memoryStream, cancellationToken);
        return await EncryptAsync(memoryStream.ToArray(), cancellationToken);
    }

    public Task<EncryptedData> EncryptAsync(byte[] plaintext, CancellationToken cancellationToken = default)
    {
        var salt = GenerateSalt(_saltSize);
        var key = DeriveKey(_masterKey, salt, 32);
        var iv = GenerateSalt(12); // GCM uses 12-byte nonce

        using var aes = new AesGcm(key, 16);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        aes.Encrypt(iv, plaintext, ciphertext, tag);

        // Combine ciphertext and tag
        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return Task.FromResult(new EncryptedData(
            Ciphertext: combined,
            IV: iv,
            Salt: salt,
            Algorithm: Algorithm,
            KeyDerivationIterations: _keyDerivationIterations));
    }

    public async Task<Stream> DecryptAsync(EncryptedData encryptedData, CancellationToken cancellationToken = default)
    {
        var decrypted = await DecryptToBytesAsync(encryptedData, cancellationToken);
        return new MemoryStream(decrypted);
    }

    public Task<byte[]> DecryptToBytesAsync(EncryptedData encryptedData, CancellationToken cancellationToken = default)
    {
        var key = DeriveKey(_masterKey, encryptedData.Salt, 32);

        using var aes = new AesGcm(key, 16);

        // Split ciphertext and tag
        var ciphertext = new byte[encryptedData.Ciphertext.Length - 16];
        var tag = new byte[16];
        Buffer.BlockCopy(encryptedData.Ciphertext, 0, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(encryptedData.Ciphertext, ciphertext.Length, tag, 0, 16);

        var plaintext = new byte[ciphertext.Length];
        aes.Decrypt(encryptedData.IV, ciphertext, tag, plaintext);

        return Task.FromResult(plaintext);
    }

    public async Task<string> EncryptStringAsync(string plaintext, CancellationToken cancellationToken = default)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = await EncryptAsync(plaintextBytes, cancellationToken);

        // Format: salt:iv:iterations:ciphertext (all base64)
        return $"{Convert.ToBase64String(encrypted.Salt)}:{Convert.ToBase64String(encrypted.IV)}:{encrypted.KeyDerivationIterations}:{Convert.ToBase64String(encrypted.Ciphertext)}";
    }

    public async Task<string> DecryptStringAsync(string encryptedBase64, CancellationToken cancellationToken = default)
    {
        var parts = encryptedBase64.Split(':');
        if (parts.Length != 4)
        {
            throw new FormatException("Invalid encrypted string format");
        }

        var encrypted = new EncryptedData(
            Ciphertext: Convert.FromBase64String(parts[3]),
            IV: Convert.FromBase64String(parts[1]),
            Salt: Convert.FromBase64String(parts[0]),
            Algorithm: Algorithm,
            KeyDerivationIterations: int.Parse(parts[2]));

        var decrypted = await DecryptToBytesAsync(encrypted, cancellationToken);
        return Encoding.UTF8.GetString(decrypted);
    }

    public byte[] DeriveKey(string password, byte[] salt, int keyLength = 32)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        return DeriveKey(passwordBytes, salt, keyLength);
    }

    private byte[] DeriveKey(byte[] key, byte[] salt, int keyLength)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(key, salt, _keyDerivationIterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(keyLength);
    }

    public Task<byte[]> GenerateKeyAsync(int keyLength = 32, CancellationToken cancellationToken = default)
    {
        var key = new byte[keyLength];
        RandomNumberGenerator.Fill(key);
        return Task.FromResult(key);
    }

    public byte[] GenerateSalt(int length = 16)
    {
        var salt = new byte[length];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }
}
