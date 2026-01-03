namespace EnterpriseDataManager.Infrastructure.Storage.TapeDevice;

public interface ITapeDeviceAdapter
{
    Task<TapeInfo> GetTapeInfoAsync(CancellationToken cancellationToken = default);
    Task<Stream> ReadAsync(long blockPosition, int blockCount, CancellationToken cancellationToken = default);
    Task WriteAsync(Stream data, CancellationToken cancellationToken = default);
    Task RewindAsync(CancellationToken cancellationToken = default);
    Task SeekAsync(long blockPosition, CancellationToken cancellationToken = default);
    Task EjectAsync(CancellationToken cancellationToken = default);
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
    Task<TapeStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

public record TapeInfo(
    string DeviceName,
    string TapeLabel,
    long CapacityBytes,
    long UsedBytes,
    int BlockSize,
    bool IsWriteProtected);

public record TapeStatus(
    bool IsReady,
    bool HasTape,
    bool IsWriteProtected,
    bool IsAtEndOfMedia,
    bool IsAtBeginningOfMedia,
    long CurrentBlockPosition,
    string? ErrorMessage);
