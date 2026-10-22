using System.Windows.Data;
using System.Windows.Markup;

namespace RiseModeMonitor.Localization;

/// <summary>Usage in XAML: Text="{loc:Loc Settings_Theme}". Updates live when the language changes.</summary>
public sealed class LocExtension : MarkupExtension
{
    public LocExtension(string key) => Key = key;

    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}
