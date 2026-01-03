namespace EnterpriseDataManager.Infrastructure.Storage.TapeDevice;

using Microsoft.Extensions.Logging;

public class MockTapeAdapter : ITapeDeviceAdapter
{
    private readonly string _deviceName;
    private readonly int _blockSize;
    private readonly long _capacityBytes;
    private readonly Dictionary<long, byte[]> _blocks = new();
    private readonly ILogger<MockTapeAdapter>? _logger;
    private long _currentPosition;
    private bool _hasTape = true;
    private bool _isWriteProtected;

    public MockTapeAdapter(
        string deviceName = "/dev/st0",
        int blockSize = 65536,
        long capacityBytes = 1024L * 1024 * 1024 * 100, // 100GB
        ILogger<MockTapeAdapter>? logger = null)
    {
        _deviceName = deviceName;
        _blockSize = blockSize;
        _capacityBytes = capacityBytes;
        _logger = logger;
    }

    public Task<TapeInfo> GetTapeInfoAsync(CancellationToken cancellationToken = default)
    {
        var usedBytes = _blocks.Values.Sum(b => (long)b.Length);

        return Task.FromResult(new TapeInfo(
            DeviceName: _deviceName,
            TapeLabel: "MOCK_TAPE_001",
            CapacityBytes: _capacityBytes,
            UsedBytes: usedBytes,
            BlockSize: _blockSize,
            IsWriteProtected: _isWriteProtected));
    }

    public Task<Stream> ReadAsync(long blockPosition, int blockCount, CancellationToken cancellationToken = default)
    {
        if (!_hasTape)
        {
            throw new InvalidOperationException("No tape in drive");
        }

        var memoryStream = new MemoryStream();

        for (var i = 0; i < blockCount; i++)
        {
            var pos = blockPosition + i;
            if (_blocks.TryGetValue(pos, out var block))
            {
                memoryStream.Write(block, 0, block.Length);
            }
            else
            {
                memoryStream.Write(new byte[_blockSize], 0, _blockSize);
            }
        }

        _currentPosition = blockPosition + blockCount;
        memoryStream.Position = 0;

        _logger?.LogDebug("Read {BlockCount} blocks from position {Position}", blockCount, blockPosition);

        return Task.FromResult<Stream>(memoryStream);
    }

    public async Task WriteAsync(Stream data, CancellationToken cancellationToken = default)
    {
        if (!_hasTape)
        {
            throw new InvalidOperationException("No tape in drive");
        }

        if (_isWriteProtected)
        {
            throw new InvalidOperationException("Tape is write protected");
        }

        using var memoryStream = new MemoryStream();
        await data.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        var blocksNeeded = (int)Math.Ceiling((double)bytes.Length / _blockSize);
        var offset = 0;

        for (var i = 0; i < blocksNeeded; i++)
        {
            var blockLength = Math.Min(_blockSize, bytes.Length - offset);
            var block = new byte[_blockSize];
            Array.Copy(bytes, offset, block, 0, blockLength);

            _blocks[_currentPosition] = block;
            _currentPosition++;
            offset += blockLength;
        }

        _logger?.LogDebug("Wrote {BlockCount} blocks at position {Position}", blocksNeeded, _currentPosition - blocksNeeded);
    }

    public Task RewindAsync(CancellationToken cancellationToken = default)
    {
        if (!_hasTape)
        {
            throw new InvalidOperationException("No tape in drive");
        }

        _currentPosition = 0;
        _logger?.LogDebug("Tape rewound to beginning");
        return Task.CompletedTask;
    }

    public Task SeekAsync(long blockPosition, CancellationToken cancellationToken = default)
    {
        if (!_hasTape)
        {
            throw new InvalidOperationException("No tape in drive");
        }

        _currentPosition = blockPosition;
        _logger?.LogDebug("Tape positioned at block {Position}", blockPosition);
        return Task.CompletedTask;
    }

    public Task EjectAsync(CancellationToken cancellationToken = default)
    {
        _hasTape = false;
        _currentPosition = 0;
        _logger?.LogDebug("Tape ejected");
        return Task.CompletedTask;
    }

    public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_hasTape);
    }

    public Task<TapeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var usedBytes = _blocks.Values.Sum(b => (long)b.Length);
        var maxBlock = _blocks.Keys.DefaultIfEmpty(-1).Max();

        return Task.FromResult(new TapeStatus(
            IsReady: true,
            HasTape: _hasTape,
            IsWriteProtected: _isWriteProtected,
            IsAtEndOfMedia: usedBytes >= _capacityBytes,
            IsAtBeginningOfMedia: _currentPosition == 0,
            CurrentBlockPosition: _currentPosition,
            ErrorMessage: null));
    }

    public void InsertTape(bool writeProtected = false)
    {
        _hasTape = true;
        _isWriteProtected = writeProtected;
        _blocks.Clear();
        _currentPosition = 0;
        _logger?.LogDebug("Tape inserted (WriteProtected: {WriteProtected})", writeProtected);
    }

    public void SetWriteProtection(bool enabled)
    {
        _isWriteProtected = enabled;
    }
}
