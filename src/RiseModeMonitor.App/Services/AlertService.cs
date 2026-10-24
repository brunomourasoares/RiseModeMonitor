using System.IO;
using System.Windows;
using System.Windows.Media;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Localization;

namespace RiseModeMonitor.Services;

/// <summary>
/// Watches every sensor snapshot, asks <see cref="AlertEvaluator"/> whether an alert is due, and
/// then plays the chosen sound and shows a tray notification.
/// </summary>
public sealed class AlertService : IDisposable
{
    private readonly ISensorService _sensors;
    private readonly IFanControlService _fans;
    private readonly ISettingsService _settings;
    private readonly TrayService _tray;
    private readonly LocalizationService _loc;
    private readonly AlertEvaluator _evaluator = new();

    // MediaPlayer must be used on the UI thread and kept alive while it plays.
    private MediaPlayer? _player;

    public AlertService(
        ISensorService sensors,
        IFanControlService fans,
        ISettingsService settings,
        TrayService tray,
        LocalizationService loc)
    {
        _sensors = sensors;
        _fans = fans;
        _settings = settings;
        _tray = tray;
        _loc = loc;
        _sensors.SnapshotUpdated += OnSnapshot;
    }

    private void OnSnapshot(object? sender, HardwareSnapshot snapshot)
    {
        var settings = _settings.Current;
        if (!settings.AlertCpuEnabled && !settings.AlertGpuEnabled && !settings.AlertFanStoppedEnabled)
            return;

        var telemetry = TelemetrySelector.Select(snapshot);
        var fans = settings.AlertFanStoppedEnabled ? _fans.GetChannels() : Array.Empty<FanChannel>();

        var events = _evaluator.Evaluate(telemetry, fans, settings, DateTimeOffset.UtcNow);
        if (events.Count == 0) return;

        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var alert in events)
            {
                var (title, message) = Describe(alert);
                _tray.ShowNotification(title, message);
            }

            PlaySound(settings.AlertSound, settings.AlertVolume);
        }));
    }

    private (string Title, string Message) Describe(AlertEvent alert) => alert.Kind switch
    {
        AlertKind.CpuTemperature => (_loc["Alert_TitleTemp"], _loc.Format("Alert_Cpu", alert.Value, alert.Threshold)),
        AlertKind.GpuTemperature => (_loc["Alert_TitleTemp"], _loc.Format("Alert_Gpu", alert.Value, alert.Threshold)),
        _ => (_loc["Alert_TitleFan"], _loc.Format("Alert_Fan", alert.Detail ?? string.Empty))
    };

    /// <summary>Plays one of the bundled alert sounds (1 to 5). Safe to call from the UI thread only.</summary>
    public void PlaySound(int sound, double volumePercent)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Sounds", $"alert-{Math.Clamp(sound, 1, 5)}.mp3");
        if (!File.Exists(path)) return;

        try
        {
            _player?.Stop();
            _player?.Close();

            _player = new MediaPlayer { Volume = Math.Clamp(volumePercent, 0, 100) / 100.0 };
            _player.Open(new Uri(path, UriKind.Absolute));
            _player.Play();
        }
        catch (Exception)
        {
            // Audio problems must never break monitoring.
        }
    }

    public void Dispose()
    {
        _sensors.SnapshotUpdated -= OnSnapshot;
        _player?.Close();
    }
}
