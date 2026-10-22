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

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new ThemeOption[]
    {
        new(ThemeMode.System, "System"),
        new(ThemeMode.Light, "Light"),
        new(ThemeMode.Dark, "Dark"),
    };

    [ObservableProperty]
    private ThemeOption _selectedTheme;

    public SettingsViewModel(ISettingsService settings, ThemeService themeService)
    {
        _settings = settings;
        _themeService = themeService;
        _selectedTheme = ThemeOptions.First(o => o.Mode == settings.Current.Theme);
    }

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        _settings.Current.Theme = value.Mode;
        _settings.Save();
        _themeService.Apply(value.Mode);
    }
}
