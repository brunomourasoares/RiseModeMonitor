namespace RiseModeMonitor.Core.Models;

public enum ThemeMode { System, Light, Dark }

/// <summary>User preferences persisted between sessions.</summary>
public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;
}
