using System.IO;
using System.Windows;
using System.Windows.Media;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Localization;

namespace RiseModeMonitor.Services;

/// <summary>
/// Watches every sensor snapshot and asks <see cref="AlertEvaluator"/> what is due: a tray notification
/// (once per occurrence) and the alert sound (repeated every 10 seconds while the condition persists).
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
        {
            // Run the evaluator once more so it forgets any old state, then stay idle.
            _evaluator.Evaluate(new DisplayTelemetry(null, null, null, null, null, null),
                Array.Empty<FanChannel>(), settings, DateTimeOffset.UtcNow);
            return;
        }

        var telemetry = TelemetrySelector.Select(snapshot);
        var fans = settings.AlertFanStoppedEnabled ? NamedChannels(settings) : Array.Empty<FanChannel>();

        var result = _evaluator.Evaluate(telemetry, fans, settings, DateTimeOffset.UtcNow);
        if (result.Notifications.Count == 0 && !result.PlaySound) return;

        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var alert in result.Notifications)
            {
                var (title, message) = Describe(alert);
                _tray.ShowNotification(title, message);
            }

            if (result.PlaySound)
                PlaySound(settings.AlertSound, settings.AlertVolume);
        }));
    }

    /// <summary>Fan channels with the names the user gave them, so the alert says "CPU_FAN1" instead of "Fan #1".</summary>
    private FanChannel[] NamedChannels(AppSettings settings) =>
        _fans.GetChannels()
            .Select(c => settings.FanNames.TryGetValue(c.Id, out var custom) && !string.IsNullOrWhiteSpace(custom)
                ? c with { Name = custom }
                : c)
            .ToArray();

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
