using CommunityToolkit.Mvvm.ComponentModel;
using RiseModeMonitor.Localization;

namespace RiseModeMonitor.ViewModels;

/// <summary>Sidebar entry whose title follows the current language.</summary>
public sealed class NavItem : ObservableObject
{
    private readonly LocalizationService _loc;
    private readonly string _titleKey;

    public NavItem(string titleKey, object viewModel, LocalizationService loc)
    {
        _titleKey = titleKey;
        _loc = loc;
        ViewModel = viewModel;
        _loc.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Title));
    }

    public string Title => _loc[_titleKey];

    public object ViewModel { get; }
}

/// <summary>"Coming soon" page whose texts follow the current language.</summary>
public sealed class PlaceholderViewModel : ObservableObject
{
    private readonly LocalizationService _loc;
    private readonly string _titleKey;
    private readonly string _descriptionKey;

    public PlaceholderViewModel(string titleKey, string descriptionKey, LocalizationService loc)
    {
        _titleKey = titleKey;
        _descriptionKey = descriptionKey;
        _loc = loc;
        _loc.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
        };
    }

    public string Title => _loc[_titleKey];

    public string Description => _loc[_descriptionKey];
}

public sealed partial class MainViewModel : ObservableObject
{
    public IReadOnlyList<NavItem> Items { get; }

    [ObservableProperty]
    private NavItem _selected;

    public MainViewModel(
        DashboardViewModel dashboard,
        HardwareViewModel hardware,
        SettingsViewModel settings,
        LocalizationService loc)
    {
        Items = new NavItem[]
        {
            new("Nav_Dashboard", dashboard, loc),
            new("Nav_Hardware", hardware, loc),
            new("Nav_Fans", new PlaceholderViewModel("Nav_Fans", "Fans_Description", loc), loc),
            new("Nav_Lighting", new PlaceholderViewModel("Nav_Lighting", "Lighting_Description", loc), loc),
            new("Nav_Settings", settings, loc),
        };
        _selected = Items[0];
    }
}
