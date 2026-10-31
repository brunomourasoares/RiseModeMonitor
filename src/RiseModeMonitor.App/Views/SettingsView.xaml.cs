using System.Windows;
using System.Windows.Controls;
using RiseModeMonitor.Localization;
using RiseModeMonitor.ViewModels;

namespace RiseModeMonitor.Views;

public partial class SettingsView : UserControl
{
    // Construtor usado quando a injeção de dependências instancia a view
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        RegisterEvent(viewModel);
    }

    // Construtor vazio essencial para o WPF não fechar a aplicação ao carregar a rota
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
        // The title follows the current language (reuses the "Settings" menu text; no hardcoded string).
        MessageBox.Show(message, LocalizationService.Instance["Nav_Settings"], MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}