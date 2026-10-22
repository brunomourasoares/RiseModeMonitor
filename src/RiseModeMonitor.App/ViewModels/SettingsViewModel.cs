using CommunityToolkit.Mvvm.ComponentModel;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Localization;
using RiseModeMonitor.Services;

namespace RiseModeMonitor.ViewModels;

/// <summary>Theme choice whose label follows the current language.</summary>
public sealed class ThemeOption : ObservableObject
{
    private readonly LocalizationService _loc;
    private readonly string _labelKey;

    public ThemeOption(ThemeMode mode, string labelKey, LocalizationService loc)
    {
        Mode = mode;
        _labelKey = labelKey;
        _loc = loc;
        _loc.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public ThemeMode Mode { get; }

    public string Label => _loc[_labelKey];
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ThemeService _themeService;
    private readonly IAutostartService _autostart;
    private readonly LocalizationService _loc;
    private bool _syncing;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; }

    public IReadOnlyList<LanguageOption> LanguageOptions => LocalizationService.Languages;

    [ObservableProperty] private ThemeOption _selectedTheme;
    [ObservableProperty] private LanguageOption _selectedLanguage;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public SettingsViewModel(
        ISettingsService settings,
        ThemeService themeService,
        IAutostartService autostart,
        LocalizationService loc)
    {
        _settings = settings;
        _themeService = themeService;
        _autostart = autostart;
        _loc = loc;

        ThemeOptions = new ThemeOption[]
        {
            new(ThemeMode.System, "Theme_System", loc),
            new(ThemeMode.Light, "Theme_Light", loc),
            new(ThemeMode.Dark, "Theme_Dark", loc),
        };

        _selectedTheme = ThemeOptions.First(o => o.Mode == settings.Current.Theme);
        _selectedLanguage = LocalizationService.Languages.FirstOrDefault(l => l.Code == settings.Current.Language)
                            ?? LocalizationService.Languages[0];
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

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        _settings.Current.Language = value.Code;
        _settings.Save();
        _loc.SetLanguage(value.Code);
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
        StatusMessage = _loc["Settings_AutostartError"];
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
