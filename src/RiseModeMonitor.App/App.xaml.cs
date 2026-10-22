using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Hardware;
using RiseModeMonitor.ViewModels;

namespace RiseModeMonitor;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<ISensorService, LibreHardwareSensorService>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        _services.GetRequiredService<ISensorService>().Start(TimeSpan.FromSeconds(1));
        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
