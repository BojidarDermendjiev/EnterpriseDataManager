using EnterpriseDataManager.Common;
using static EnterpriseDataManager.Common.EntityValidationConstants;

namespace EnterpriseDataManager.Core.ValueObjects;

public sealed class StoragePath : ValueObject
{
    private static readonly char[] InvalidChars = System.IO.Path.GetInvalidPathChars();

    public string Path { get; }
    public bool IsAbsolute { get; }

    private StoragePath(string path)
    {
        Path = NormalizePath(path);
        IsAbsolute = System.IO.Path.IsPathRooted(Path);
    }

    public static StoragePath Create(string path)
    {
        Guard.AgainstNullOrWhiteSpace(path, PathCannotBeEmpty);

        if (path.IndexOfAny(InvalidChars) >= 0)
            throw new ArgumentException(PathContainsInvalidCharacters, nameof(path));

        return new StoragePath(path);
    }

    public StoragePath Combine(string relativePath)
    {
        Guard.AgainstNullOrWhiteSpace(relativePath, RelativePathCannotBeEmpty);
        return Create(System.IO.Path.Combine(Path, relativePath));
    }

    public string GetFileName() => System.IO.Path.GetFileName(Path);
    public string GetDirectory() => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
    public string GetExtension() => System.IO.Path.GetExtension(Path);

    private static string NormalizePath(string path)
    {
        return path.Trim()
            .Replace(System.IO.Path.DirectorySeparatorChar, '/')
            .TrimEnd('/');
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Path.ToLowerInvariant();
    }

    public override string ToString() => Path;

    public static implicit operator string(StoragePath storagePath) => storagePath.Path;
}
