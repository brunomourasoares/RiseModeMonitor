using CommunityToolkit.Mvvm.ComponentModel;

namespace RiseModeMonitor.ViewModels;

public sealed record NavItem(string Title, object ViewModel);

public sealed record PlaceholderViewModel(string Title, string Description);

public sealed partial class MainViewModel : ObservableObject
{
    public IReadOnlyList<NavItem> Items { get; }

    [ObservableProperty]
    private NavItem _selected;

    public MainViewModel(DashboardViewModel dashboard, HardwareViewModel hardware, SettingsViewModel settings)
    {
        Items = new NavItem[]
        {
            new("Dashboard", dashboard),
            new("Hardware", hardware),
            new("Fans", new PlaceholderViewModel("Fans", "Fan speed control and curves. Coming soon.")),
            new("Lighting", new PlaceholderViewModel("Lighting", "RGB / ARGB colors and effects. Coming soon.")),
            new("Settings", settings),
        };
        _selected = Items[0];
    }
}
