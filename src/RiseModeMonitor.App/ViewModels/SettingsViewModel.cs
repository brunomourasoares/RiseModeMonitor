using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows; // Necessário para o MessageBox
using CommunityToolkit.Mvvm.ComponentModel;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Localization;
using RiseModeMonitor.Services;

namespace RiseModeMonitor.ViewModels;

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

    // Evento limpo para avisar a View quando der erro
    public event Action<string>? OnErrorOccurred;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; }
    public IReadOnlyList<LanguageOption> LanguageOptions => LocalizationService.Languages;
    public AlertSettingsViewModel Alerts { get; }

    [ObservableProperty] private ThemeOption _selectedTheme;
    [ObservableProperty] private LanguageOption _selectedLanguage;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private bool _displayEnabled;

    public SettingsViewModel(
        ISettingsService settings,
        ThemeService themeService,
        IAutostartService autostart,
        LocalizationService loc,
        AlertSettingsViewModel alerts)
    {
        _settings = settings;
        _themeService = themeService;
        _autostart = autostart;
        _loc = loc;
        Alerts = alerts;

        ThemeOptions = new ThemeOption[]
        {
            new(ThemeMode.System, "Theme_System", loc),
            new(ThemeMode.Light, "Theme_Light", loc),
            new(ThemeMode.Dark, "Theme_Dark", loc),
        };

        var savedLanguage = LocalizationService.Normalize(settings.Current.Language);

        _selectedTheme = ThemeOptions.First(o => o.Mode == settings.Current.Theme);
        _selectedLanguage = LocalizationService.Languages.FirstOrDefault(l => l.Code == savedLanguage)
                            ?? LocalizationService.Languages[0];
        
        _syncing = true;
        _startWithWindows = autostart.IsEnabled();
        _syncing = false;

        _startMinimized = settings.Current.StartMinimized;
        _closeToTray = settings.Current.CloseToTray;
        _displayEnabled = settings.Current.DisplayEnabled;
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
            return;
        }

        // Reverte o checkbox e dispara o evento de erro
        _syncing = true;
        StartWithWindows = !value;
        _syncing = false;

        OnErrorOccurred?.Invoke(_loc["Settings_AutostartError"]);
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

    partial void OnDisplayEnabledChanged(bool value)
    {
        _settings.Current.DisplayEnabled = value;
        _settings.Save();
    }
}