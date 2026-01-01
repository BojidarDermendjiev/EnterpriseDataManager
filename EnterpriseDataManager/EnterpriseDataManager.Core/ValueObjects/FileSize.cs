namespace EnterpriseDataManager.Core.ValueObjects;

using EnterpriseDataManager.Common;
using static EnterpriseDataManager.Common.EntityValidationConstants;
public sealed class FileSize : ValueObject, IComparable<FileSize>
{
    private const long BytesPerKilobyte = 1024;
    private const long BytesPerMegabyte = BytesPerKilobyte * 1024;
    private const long BytesPerGigabyte = BytesPerMegabyte * 1024;
    private const long BytesPerTerabyte = BytesPerGigabyte * 1024;

    public long Bytes { get; }

    private FileSize(long bytes)
    {
        Bytes = bytes;
    }

    public static FileSize FromBytes(long bytes)
    {
        Guard.AgainstNegative(bytes, SizeCannotBeNegative);
        return new FileSize(bytes);
    }

    public static FileSize FromKilobytes(double kb) => FromBytes((long)(kb * BytesPerKilobyte));
    public static FileSize FromMegabytes(double mb) => FromBytes((long)(mb * BytesPerMegabyte));
    public static FileSize FromGigabytes(double gb) => FromBytes((long)(gb * BytesPerGigabyte));
    public static FileSize FromTerabytes(double tb) => FromBytes((long)(tb * BytesPerTerabyte));

    public static FileSize Zero => new(0);

    public double ToKilobytes() => (double)Bytes / BytesPerKilobyte;
    public double ToMegabytes() => (double)Bytes / BytesPerMegabyte;
    public double ToGigabytes() => (double)Bytes / BytesPerGigabyte;
    public double ToTerabytes() => (double)Bytes / BytesPerTerabyte;

    public static FileSize operator +(FileSize left, FileSize right)
        => FromBytes(left.Bytes + right.Bytes);

    public static FileSize operator -(FileSize left, FileSize right)
        => FromBytes(Math.Max(0, left.Bytes - right.Bytes));

    public int CompareTo(FileSize? other) => other is null ? 1 : Bytes.CompareTo(other.Bytes);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Bytes;
    }

    public override string ToString()
    {
        return Bytes switch
        {
            >= BytesPerTerabyte => $"{ToTerabytes():F2} TB",
            >= BytesPerGigabyte => $"{ToGigabytes():F2} GB",
            >= BytesPerMegabyte => $"{ToMegabytes():F2} MB",
            >= BytesPerKilobyte => $"{ToKilobytes():F2} KB",
            _ => $"{Bytes} B"
        };
    }
}
