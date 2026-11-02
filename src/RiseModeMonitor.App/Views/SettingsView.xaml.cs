using System.Windows;
using System.Windows.Controls;
using RiseModeMonitor.Localization;
using RiseModeMonitor.ViewModels;

namespace RiseModeMonitor.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        RegisterEvent(viewModel);
    }

    public SettingsView()
    {
        InitializeComponent();

        if (DataContext is SettingsViewModel viewModel)
        {
            RegisterEvent(viewModel);
        }
        else
        {
            DataContextChanged += (s, e) =>
            {
                if (e.NewValue is SettingsViewModel vm)
                {
                    RegisterEvent(vm);
                }
            };
        }
    }

    private void RegisterEvent(SettingsViewModel viewModel)
    {
        viewModel.OnErrorOccurred -= OnErrorReceived;
        viewModel.OnErrorOccurred += OnErrorReceived;
    }

    private void OnErrorReceived(string message)
    {
        MessageBox.Show(message, LocalizationService.Instance["Nav_Settings"], MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}