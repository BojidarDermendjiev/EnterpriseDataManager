namespace EnterpriseDataManager.Infrastructure.Storage.TapeDevice;

using Microsoft.Extensions.Logging;
using System.Diagnostics;

public class TapeDeviceAdapter : ITapeDeviceAdapter
{
    private readonly string _devicePath;
    private readonly int _blockSize;
    private readonly ILogger<TapeDeviceAdapter>? _logger;
    private long _currentPosition;

    public TapeDeviceAdapter(string devicePath, int blockSize = 65536, ILogger<TapeDeviceAdapter>? logger = null)
    {
        _devicePath = devicePath ?? throw new ArgumentNullException(nameof(devicePath));
        _blockSize = blockSize;
        _logger = logger;
    }

    public async Task<TapeInfo> GetTapeInfoAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);

        return new TapeInfo(
            DeviceName: _devicePath,
            TapeLabel: await GetTapeLabelAsync(cancellationToken),
            CapacityBytes: await GetCapacityAsync(cancellationToken),
            UsedBytes: await GetUsedBytesAsync(cancellationToken),
            BlockSize: _blockSize,
            IsWriteProtected: status.IsWriteProtected);
    }

    public async Task<Stream> ReadAsync(long blockPosition, int blockCount, CancellationToken cancellationToken = default)
    {
        if (blockPosition != _currentPosition)
        {
            await SeekAsync(blockPosition, cancellationToken);
        }

        var result = await ExecuteTapeCommandAsync("read", $"-bs={_blockSize} -count={blockCount}", cancellationToken);
        _currentPosition += blockCount;

        var memoryStream = new MemoryStream();
        if (!string.IsNullOrEmpty(result))
        {
            var bytes = Convert.FromBase64String(result);
            await memoryStream.WriteAsync(bytes, cancellationToken);
            memoryStream.Position = 0;
        }

        return memoryStream;
    }

    public async Task WriteAsync(Stream data, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await data.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        var blocksWritten = (int)Math.Ceiling((double)bytes.Length / _blockSize);

        _logger?.LogInformation("Writing {Bytes} bytes ({Blocks} blocks) to tape at position {Position}",
            bytes.Length, blocksWritten, _currentPosition);

        await ExecuteTapeCommandAsync("write", $"-bs={_blockSize}", cancellationToken, Convert.ToBase64String(bytes));
        _currentPosition += blocksWritten;
    }

    public async Task RewindAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteTapeCommandAsync("rewind", string.Empty, cancellationToken);
        _currentPosition = 0;
        _logger?.LogInformation("Tape rewound to beginning");
    }

    public async Task SeekAsync(long blockPosition, CancellationToken cancellationToken = default)
    {
        await ExecuteTapeCommandAsync("seek", $"-block={blockPosition}", cancellationToken);
        _currentPosition = blockPosition;
        _logger?.LogInformation("Tape positioned at block {Position}", blockPosition);
    }

    public async Task EjectAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteTapeCommandAsync("eject", string.Empty, cancellationToken);
        _currentPosition = 0;
        _logger?.LogInformation("Tape ejected");
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        return status.IsReady && status.HasTape;
    }

    public async Task<TapeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExecuteTapeCommandAsync("status", string.Empty, cancellationToken);

            return new TapeStatus(
                IsReady: true,
                HasTape: true,
                IsWriteProtected: result.Contains("WP", StringComparison.OrdinalIgnoreCase),
                IsAtEndOfMedia: result.Contains("EOM", StringComparison.OrdinalIgnoreCase),
                IsAtBeginningOfMedia: _currentPosition == 0,
                CurrentBlockPosition: _currentPosition,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return new TapeStatus(
                IsReady: false,
                HasTape: false,
                IsWriteProtected: false,
                IsAtEndOfMedia: false,
                IsAtBeginningOfMedia: false,
                CurrentBlockPosition: 0,
                ErrorMessage: ex.Message);
        }
    }

    private async Task<string> GetTapeLabelAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteTapeCommandAsync("label", string.Empty, cancellationToken);
        }
        catch
        {
            return "Unknown";
        }
    }

    private async Task<long> GetCapacityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteTapeCommandAsync("capacity", string.Empty, cancellationToken);
            return long.TryParse(result, out var capacity) ? capacity : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<long> GetUsedBytesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteTapeCommandAsync("used", string.Empty, cancellationToken);
            return long.TryParse(result, out var used) ? used : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<string> ExecuteTapeCommandAsync(string command, string args, CancellationToken cancellationToken, string? input = null)
    {
        var isWindows = OperatingSystem.IsWindows();
        var processInfo = new ProcessStartInfo
        {
            FileName = isWindows ? "mt.exe" : "mt",
            Arguments = $"-f {_devicePath} {command} {args}".Trim(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input != null,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };
        process.Start();

        if (input != null)
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Tape command failed: {error}");
        }

        return output.Trim();
    }
}
