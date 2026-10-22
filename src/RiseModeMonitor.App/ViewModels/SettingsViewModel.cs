using CommunityToolkit.Mvvm.ComponentModel;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Services;

namespace RiseModeMonitor.ViewModels;

public sealed record ThemeOption(ThemeMode Mode, string Label);

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ThemeService _themeService;
    private readonly IAutostartService _autostart;
    private bool _syncing;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new ThemeOption[]
    {
        new(ThemeMode.System, "System"),
        new(ThemeMode.Light, "Light"),
        new(ThemeMode.Dark, "Dark"),
    };

    [ObservableProperty] private ThemeOption _selectedTheme;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public SettingsViewModel(ISettingsService settings, ThemeService themeService, IAutostartService autostart)
    {
        _settings = settings;
        _themeService = themeService;
        _autostart = autostart;

        _selectedTheme = ThemeOptions.First(o => o.Mode == settings.Current.Theme);
        _startWithWindows = autostart.IsEnabled(); // real system state, not a stored flag
        _startMinimized = settings.Current.StartMinimized;
        _closeToTray = settings.Current.CloseToTray;
    }

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        _settings.Current.Theme = value.Mode;
        _settings.Save();
        _themeService.Apply(value.Mode);
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_syncing) return;

        if (_autostart.SetEnabled(value))
        {
            StatusMessage = string.Empty;
            return;
        }

        // Revert the checkbox and explain.
        _syncing = true;
        StartWithWindows = !value;
        _syncing = false;
        StatusMessage = "Could not update the startup task. Run the app as administrator and try again.";
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        _settings.Current.StartMinimized = value;
        _settings.Save();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        _settings.Current.CloseToTray = value;
        _settings.Save();
    }
}
