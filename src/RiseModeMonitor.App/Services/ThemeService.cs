using System.Windows;
using Microsoft.Win32;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RiseModeMonitor.Services;

/// <summary>
/// Applies Light/Dark to both the WPF-UI controls and the Rise Mode brand palette.
/// In System mode it follows the Windows "app mode" setting and reacts when it changes.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private readonly ISettingsService _settings;

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Apply(Core.Models.ThemeMode mode)
    {
        var effective = mode == Core.Models.ThemeMode.System
            ? (SystemUsesLightTheme() ? Core.Models.ThemeMode.Light : Core.Models.ThemeMode.Dark)
            : mode;

        // 1) Brand palette (our own brushes: BgBrush, SurfaceBrush, AccentBrush...).
        var uri = new Uri($"pack://application:,,,/RiseModeMonitor;component/Themes/{effective}.xaml", UriKind.Absolute);
        var merged = Application.Current.Resources.MergedDictionaries;
        var index = merged.ToList().FindIndex(d =>
            d.Source?.OriginalString.Contains("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase) == true ||
            d.Source?.OriginalString.Contains("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) == true);

        var brand = new ResourceDictionary { Source = uri };
        if (index >= 0) merged[index] = brand;
        else merged.Add(brand);

        // 2) WPF-UI control theme (no Mica: the window uses the solid brand background).
        ApplicationThemeManager.Apply(
            effective == Core.Models.ThemeMode.Light ? ApplicationTheme.Light : ApplicationTheme.Dark,
            WindowBackdropType.None,
            true);
    }

    private static bool SystemUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_settings.Current.Theme != Core.Models.ThemeMode.System) return;
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color)) return;

        Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(Core.Models.ThemeMode.System)));
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
