namespace RiseModeMonitor.Core.Models;

public enum ThemeMode { System, Light, Dark }

/// <summary>User preferences persisted between sessions.</summary>
public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    /// <summary>UI language code: en (default), pt, es, it, fr, zh-Hans.</summary>
    public string Language { get; set; } = "en";

    /// <summary>When launched by the startup task (--minimized), stay hidden in the tray.</summary>
    public bool StartMinimized { get; set; } = true;

    /// <summary>The window close button hides the app to the tray instead of exiting.</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Send CPU/GPU/RAM telemetry to the water cooler block display.</summary>
    public bool DisplayEnabled { get; set; } = true;
}
