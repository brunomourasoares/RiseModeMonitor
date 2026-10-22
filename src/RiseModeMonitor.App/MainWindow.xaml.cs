using System.Windows;
using RiseModeMonitor.ViewModels;

namespace RiseModeMonitor;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
