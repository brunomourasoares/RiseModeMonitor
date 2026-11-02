namespace RiseModeMonitor.Core.Models;

public enum ThemeMode { System, Light, Dark }

/// <summary>User preferences persisted between sessions.</summary>
public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    /// <summary>UI language code: en (default), pt, es, it, fr, zh. Older files may hold zh-Hans (mapped to zh).</summary>
    public string Language { get; set; } = "en";

    /// <summary>When launched by the startup task (--minimized), stay hidden in the tray.</summary>
    public bool StartMinimized { get; set; } = true;

    /// <summary>The window close button hides the app to the tray instead of exiting.</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Send CPU/GPU/RAM telemetry to the water cooler block display.</summary>
    public bool DisplayEnabled { get; set; } = true;

    /// <summary>
    /// Custom fan names chosen by the user (e.g. CPU_FAN1), keyed by the fan channel id.
    /// The hardware library cannot tell which motherboard header a channel is wired to.
    /// </summary>
    public Dictionary<string, string> FanNames { get; set; } = new();

    /// <summary>
    /// Ids of the fan channels that were seen spinning (RPM above 0) at least once. It only decides the default of
    /// <see cref="IsFanAlertEnabled"/>: a channel that spun before is expected to keep spinning.
    /// </summary>
    public HashSet<string> FansSeenSpinning { get; set; } = new();

    /// <summary>
    /// The user's choice per fan channel (id to on/off) for the stopped-fan alert. A channel with no entry follows
    /// the default described in <see cref="IsFanAlertEnabled"/>.
    /// </summary>
    public Dictionary<string, bool> FanAlerts { get; set; } = new();

    /// <summary>Hide the fan cards whose alert is turned off (an empty header is off by default).</summary>
    public bool HideDisabledFans { get; set; } = true;

    // The sensor timer thread (alerts) reads these sets while the UI thread updates them.
    private readonly object _fanGate = new();

    /// <summary>
    /// True when a stopped fan must raise the alert: the user's choice if there is one, otherwise on for a channel
    /// that was seen spinning and off for one that never was (empty headers always read 0 RPM).
    /// </summary>
    public bool IsFanAlertEnabled(string channelId)
    {
        lock (_fanGate)
            return FanAlerts.TryGetValue(channelId, out var on) ? on : FansSeenSpinning.Contains(channelId);
    }

    public void SetFanAlert(string channelId, bool enabled)
    {
        lock (_fanGate) FanAlerts[channelId] = enabled;
    }

    /// <summary>Remembers that the channel spun. Returns true the first time, so the caller can save.</summary>
    public bool MarkFanSeenSpinning(string channelId)
    {
        lock (_fanGate) return FansSeenSpinning.Add(channelId);
    }

    // ---- Alerts (all off by default) ----

    /// <summary>Alert when the CPU temperature reaches <see cref="AlertCpuThreshold"/>.</summary>
    public bool AlertCpuEnabled { get; set; }

    public double AlertCpuThreshold { get; set; } = 85;

    /// <summary>Alert when the GPU temperature reaches <see cref="AlertGpuThreshold"/>.</summary>
    public bool AlertGpuEnabled { get; set; }

    public double AlertGpuThreshold { get; set; } = 85;

    /// <summary>Alert when a fan that was spinning stops (0 RPM).</summary>
    public bool AlertFanStoppedEnabled { get; set; }

    /// <summary>Which of the bundled sounds to play: 1 to 12 (alert-N.mp3).</summary>
    public int AlertSound { get; set; } = 1;

    /// <summary>Alert volume, 0 to 100.</summary>
    public double AlertVolume { get; set; } = 70;

    /// <summary>
    /// Seconds between two plays of the alert sound while an alert persists (3 to 120). Older settings files may
    /// still hold the removed AlertSoundDurationSeconds; it is ignored.
    /// </summary>
    public int AlertSoundIntervalSeconds { get; set; } = 10;
}
