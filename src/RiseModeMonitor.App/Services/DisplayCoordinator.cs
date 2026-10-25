using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.Services;

/// <summary>
/// Forwards every sensor snapshot to the water cooler display, when enabled in Settings.
/// Runs on the sensor polling thread (once per snapshot).
/// </summary>
public sealed class DisplayCoordinator : IDisposable
{
    private readonly ISensorService _sensors;
    private readonly IDisplayService _display;
    private readonly ISettingsService _settings;

    public DisplayCoordinator(ISensorService sensors, IDisplayService display, ISettingsService settings)
    {
        _sensors = sensors;
        _display = display;
        _settings = settings;
        _sensors.SnapshotUpdated += OnSnapshot;
    }

    private void OnSnapshot(object? sender, HardwareSnapshot snapshot)
    {
        if (!_settings.Current.DisplayEnabled) return;

        // The result (connected, not found...) is written to hid-diagnostic.log by the HID service.
        _display.Send(TelemetrySelector.Select(snapshot));
    }

    public void Dispose() => _sensors.SnapshotUpdated -= OnSnapshot;
}
