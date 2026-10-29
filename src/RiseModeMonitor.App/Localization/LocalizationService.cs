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

    /// <summary>
    /// Native names (with a region code) are intentionally not translated. Codes match the satellite assembly folders
    /// of the build output (en is the neutral resource inside the exe).
    /// </summary>
    public static IReadOnlyList<LanguageOption> Languages { get; } = new LanguageOption[]
    {
        new("en", "English (US)"),
        new("pt", "Português (BR)"),
        new("es", "Español (ES)"),
        new("it", "Italiano (IT)"),
        new("fr", "Français (FR)"),
        new("zh", "简体中文 (CN)"),
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

    /// <summary>Maps codes saved by older versions (zh-Hans) to the current ones (zh).</summary>
    public static string Normalize(string? code) => code switch
    {
        "zh-Hans" or "zh-CN" or "zh-CHS" => "zh",
        null or "" => "en",
        _ => code
    };

    public void SetLanguage(string? code)
    {
        var normalized = Normalize(code);
        var target = Languages.Any(l => l.Code == normalized) ? normalized : "en";
        Culture = CultureInfo.GetCultureInfo(target);

        // "Item[]" is the indexer change notification WPF listens to.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
