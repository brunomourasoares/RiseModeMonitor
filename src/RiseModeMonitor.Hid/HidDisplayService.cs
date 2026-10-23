using System.IO;
using HidSharp;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.Hid;

/// <summary>
/// Sends the observed 11-byte telemetry payload to the Rise Mode water cooler display over HID.
/// Fan and pump control commands are intentionally NOT implemented: no safe, validated protocol
/// is known for them (see docs/display-protocol.md).
/// </summary>
public sealed class HidDisplayService : IDisplayService
{
    public const int VendorId = 0xAA88;
    public const int ProductId = 0x8666;

    // The physical device advertises an 11-byte output report; the payload is written as-is
    // (no extra report-ID byte). Interfaces with another length are rejected.
    public const int RequiredOutputReportLength = DisplayReport.PayloadLength;

    private static readonly DiagnosticMessageThrottle DiagnosticThrottle =
        new(TimeSpan.FromMinutes(1), maxTrackedMessages: 128);

    private readonly object _sync = new();
    private HidStream? _stream;
    private DateTime _nextDeviceDiscoveryAt = DateTime.MinValue;
    private bool _disposed;

    public DisplayStatus Send(DisplayTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        byte[] payload;
        try
        {
            payload = DisplayReport.Create(telemetry);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Log($"Invalid telemetry: {ex.Message}");
            return DisplayStatus.InvalidTelemetry;
        }

        lock (_sync)
        {
            if (_disposed) return DisplayStatus.Error;

            try
            {
                if (_stream is null)
                {
                    var status = TryOpen();
                    if (status is not null) return status.Value;
                }

                _stream!.Write(payload, 0, payload.Length);
                _stream.Flush();

                Log($"HID write completed without exception. Bytes={payload.Length}. Device acknowledgement not verified.");
                return DisplayStatus.Sent;
            }
            catch (Exception ex)
            {
                Log($"HID communication failed: {ex}");
                ResetStream();
                return DisplayStatus.Error;
            }
        }
    }

    /// <summary>Opens a compatible interface. Returns null on success, or the failure status.</summary>
    private DisplayStatus? TryOpen()
    {
        // Avoid enumerating HID devices on every poll while the display is unplugged.
        if (DateTime.UtcNow < _nextDeviceDiscoveryAt)
            return DisplayStatus.DeviceNotFound;

        var devices = DeviceList.Local.GetHidDevices(VendorId, ProductId).ToArray();
        Log($"Found {devices.Length} HID interface(s) for VID=0x{VendorId:X4}, PID=0x{ProductId:X4}.");

        if (devices.Length == 0)
        {
            _nextDeviceDiscoveryAt = DateTime.UtcNow.AddSeconds(2);
            return DisplayStatus.DeviceNotFound;
        }

        var foundCompatible = false;
        var inspectionFailed = false;

        foreach (var device in devices)
        {
            try
            {
                var outputLength = device.GetMaxOutputReportLength();
                Log($"Path={device.DevicePath}, Output={outputLength}, Input={device.GetMaxInputReportLength()}, Required={RequiredOutputReportLength}");

                if (outputLength != RequiredOutputReportLength)
                {
                    Log($"Interface skipped: output report length {outputLength} != {RequiredOutputReportLength}.");
                    continue;
                }

                foundCompatible = true;

                if (!device.TryOpen(out var opened))
                {
                    Log($"Could not open HID interface: {device.DevicePath}");
                    continue;
                }

                _stream = opened;
                Log($"HID interface opened. Output report length: {outputLength} bytes.");
                return null;
            }
            catch (Exception ex)
            {
                inspectionFailed = true;
                Log($"HID interface inspection failed: {ex}");
            }
        }

        // Retry discovery later instead of hammering the HID stack every second.
        _nextDeviceDiscoveryAt = DateTime.UtcNow.AddSeconds(2);

        if (foundCompatible) return DisplayStatus.OpenFailed;
        return inspectionFailed ? DisplayStatus.Error : DisplayStatus.IncompatibleReport;
    }

    private void ResetStream()
    {
        try { _stream?.Dispose(); }
        catch (Exception ex) { Log($"Error disposing HID stream: {ex.Message}"); }

        _stream = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            ResetStream();
        }
    }

    private static void Log(string message)
    {
        try
        {
            if (!DiagnosticThrottle.ShouldWrite(message, DateTime.UtcNow)) return;

            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RiseModeMonitor");
            Directory.CreateDirectory(directory);

            File.AppendAllText(
                Path.Combine(directory, "hid-diagnostic.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging failures must never interrupt HID communication.
        }
    }
}
