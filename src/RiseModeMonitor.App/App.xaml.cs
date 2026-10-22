using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Hardware;
using RiseModeMonitor.Services;
using RiseModeMonitor.ViewModels;

namespace RiseModeMonitor;

public partial class App : Application
{
    private const string StartMinimizedArg = "--minimized";

    private ServiceProvider? _services;
    private Mutex? _singleInstance;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one instance: two copies would fight over the sensor driver and the tray.
        _singleInstance = new Mutex(true, @"Local\RiseModeMonitor.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        // The window can be hidden in the tray, so the app only ends on an explicit Shutdown().
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IAutostartService, TaskSchedulerAutostartService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<TrayService>();
        services.AddSingleton<ISensorService, LibreHardwareSensorService>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<HardwareViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        var settings = _services.GetRequiredService<ISettingsService>();
        _services.GetRequiredService<ThemeService>().Apply(settings.Current.Theme);
        _services.GetRequiredService<ISensorService>().Start(TimeSpan.FromSeconds(1));

        var window = _services.GetRequiredService<MainWindow>();

        window.Closing += (_, args) =>
        {
            if (_exiting || !settings.Current.CloseToTray) return;
            args.Cancel = true;
            window.Hide();
        };
        window.Closed += (_, _) => Shutdown();

        var tray = _services.GetRequiredService<TrayService>();
        tray.OpenRequested += (_, _) => ShowWindow(window);
        tray.ExitRequested += (_, _) =>
        {
            _exiting = true;
            Shutdown();
        };

        var startHidden = e.Args.Contains(StartMinimizedArg) && settings.Current.StartMinimized;
        if (!startHidden) window.Show();
    }

    private static void ShowWindow(Window window)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
