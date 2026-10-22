using RiseModeMonitor.ViewModels;
using Wpf.Ui.Controls;

namespace RiseModeMonitor;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
