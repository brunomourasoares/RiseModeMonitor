using HidSharp;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.Hid;

public sealed record HidDeviceOptions(int VendorId, int ProductId);

/// <summary>
/// Water cooler display over HID. Skeleton only: connection logic is in place,
/// the packet protocol is still TODO (see the HID issue).
/// </summary>
public sealed class HidDisplayService : IDisplayService, IDisposable
{
    private readonly HidDeviceOptions _options;
    private HidStream? _stream;

    public HidDisplayService(HidDeviceOptions options) => _options = options;

    public bool IsConnected => _stream is not null;

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var device = DeviceList.Local.GetHidDeviceOrNull(_options.VendorId, _options.ProductId);
        if (device is null || !device.TryOpen(out var stream))
            return Task.FromResult(false);

        _stream = stream;
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        _stream?.Dispose();
        _stream = null;
        return Task.CompletedTask;
    }

    public void Dispose() => _stream?.Dispose();
}
