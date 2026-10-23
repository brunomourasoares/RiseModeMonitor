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

    /// <summary>Result of the last write; null until the first attempt (or while disabled).</summary>
    public DisplayStatus? LastStatus { get; private set; }

    /// <summary>Raised on a background thread when <see cref="LastStatus"/> changes.</summary>
    public event EventHandler? StatusChanged;

    private void OnSnapshot(object? sender, HardwareSnapshot snapshot)
    {
        if (!_settings.Current.DisplayEnabled)
        {
            SetStatus(null);
            return;
        }

        SetStatus(_display.Send(TelemetrySelector.Select(snapshot)));
    }

    private void SetStatus(DisplayStatus? status)
    {
        if (LastStatus == status) return;
        LastStatus = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _sensors.SnapshotUpdated -= OnSnapshot;
}
