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

    // ---- Alerts (all off by default) ----

    /// <summary>Alert when the CPU temperature reaches <see cref="AlertCpuThreshold"/>.</summary>
    public bool AlertCpuEnabled { get; set; }

    public double AlertCpuThreshold { get; set; } = 85;

    /// <summary>Alert when the GPU temperature reaches <see cref="AlertGpuThreshold"/>.</summary>
    public bool AlertGpuEnabled { get; set; }

    public double AlertGpuThreshold { get; set; } = 85;

    /// <summary>Alert when a fan that was spinning stops (0 RPM).</summary>
    public bool AlertFanStoppedEnabled { get; set; }

    /// <summary>Which of the bundled sounds to play: 1 to 5 (alert-N.mp3).</summary>
    public int AlertSound { get; set; } = 1;

    /// <summary>Alert volume, 0 to 100.</summary>
    public double AlertVolume { get; set; } = 70;
}
