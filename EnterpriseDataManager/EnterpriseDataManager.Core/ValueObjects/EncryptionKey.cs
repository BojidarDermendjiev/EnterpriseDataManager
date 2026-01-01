namespace EnterpriseDataManager.Core.ValueObjects;

using EnterpriseDataManager.Common;
using static EnterpriseDataManager.Common.EntityValidationConstants;
public sealed class EncryptionKey : ValueObject
{
    public string KeyId { get; }
    public string Algorithm { get; }
    public int KeySizeBits { get; }

    private EncryptionKey(string keyId, string algorithm, int keySizeBits)
    {
        KeyId = keyId;
        Algorithm = algorithm;
        KeySizeBits = keySizeBits;
    }

    public static EncryptionKey Create(string keyId, string algorithm = "AES-256-GCM", int keySizeBits = 256)
    {
        Guard.AgainstNullOrWhiteSpace(keyId, KeyIdCannotBeEmpty);
        Guard.AgainstNullOrWhiteSpace(algorithm, AlgorithmCannotBeEmpty);
        Guard.AgainstOutOfRange(keySizeBits, 128, 4096, KeySizeMustBeInRange);

        return new EncryptionKey(keyId.Trim(), algorithm.Trim().ToUpperInvariant(), keySizeBits);
    }

    public static EncryptionKey Aes256(string keyId) => Create(keyId, "AES-256-GCM", 256);
    public static EncryptionKey Aes128(string keyId) => Create(keyId, "AES-128-GCM", 128);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return KeyId;
        yield return Algorithm;
        yield return KeySizeBits;
    }

    public override string ToString() => $"{Algorithm} ({KeySizeBits}-bit): {KeyId}";
}
