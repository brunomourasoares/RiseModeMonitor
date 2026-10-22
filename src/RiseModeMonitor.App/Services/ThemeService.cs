using System.Windows;
using Microsoft.Win32;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.Services;

/// <summary>
/// Applies the Light/Dark resource dictionary. In System mode it follows the Windows
/// "app mode" setting and reacts when the user changes it.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private readonly ISettingsService _settings;

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Apply(ThemeMode mode)
    {
        var effective = mode == ThemeMode.System
            ? (SystemUsesLightTheme() ? ThemeMode.Light : ThemeMode.Dark)
            : mode;

        var uri = new Uri($"pack://application:,,,/RiseModeMonitor;component/Themes/{effective}.xaml", UriKind.Absolute);

        // Index 0 of App.xaml's merged dictionaries is the active theme.
        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = uri };
    }

    private static bool SystemUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_settings.Current.Theme != ThemeMode.System) return;
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color)) return;

        Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(ThemeMode.System)));
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
