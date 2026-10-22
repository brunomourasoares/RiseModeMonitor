using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace RiseModeMonitor.Localization;

public sealed record LanguageOption(string Code, string Name);

/// <summary>
/// Looks up UI strings from Resources/Strings*.resx and notifies the UI when the language changes,
/// so XAML bindings ({loc:Loc Key}) update live without restarting the app.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    /// <summary>Native names are intentionally not translated.</summary>
    public static IReadOnlyList<LanguageOption> Languages { get; } = new LanguageOption[]
    {
        new("en", "English"),
        new("pt", "Português"),
        new("es", "Español"),
        new("it", "Italiano"),
        new("fr", "Français"),
        new("zh-Hans", "简体中文"),
    };

    private readonly ResourceManager _resources =
        new("RiseModeMonitor.Resources.Strings", typeof(LocalizationService).Assembly);

    private LocalizationService() { }

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changed; view models use it to refresh computed texts.</summary>
    public event EventHandler? LanguageChanged;

    public string this[string key] => _resources.GetString(key, Culture) ?? $"[{key}]";

    public string Format(string key, params object[] args) => string.Format(Culture, this[key], args);

    public void SetLanguage(string? code)
    {
        var target = Languages.Any(l => l.Code == code) ? code! : "en";
        Culture = CultureInfo.GetCultureInfo(target);

        // "Item[]" is the indexer change notification WPF listens to.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
