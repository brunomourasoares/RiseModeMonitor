using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Localization;

namespace RiseModeMonitor.ViewModels;

public sealed record DriveViewModel(string Name, double UsedPercent, string Summary);

public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly ISensorService _sensors;
    private readonly LocalizationService _loc;
    private int _tick;

    [ObservableProperty] private string _cpuName = "CPU";
    [ObservableProperty] private string _cpuTemperature = "--";
    [ObservableProperty] private string _cpuLoadText = "--";
    [ObservableProperty] private double _cpuLoad;

    [ObservableProperty] private string _gpuName = "GPU";
    [ObservableProperty] private string _gpuTemperature = "--";
    [ObservableProperty] private string _gpuLoadText = "--";
    [ObservableProperty] private double _gpuLoad;

    [ObservableProperty] private string _memoryText = "--";
    [ObservableProperty] private string _memoryUsedText = "--";
    [ObservableProperty] private double _memoryLoad;

    /// <summary>
    /// The CPU temperature exactly as the water cooler display receives it, as digits only
    /// (the 7-segment font has no letters or symbols). Padded to 3 characters; blank when unknown.
    /// </summary>
    [ObservableProperty] private string _displayPreviewText = "   ";

    public ObservableCollection<DriveViewModel> Drives { get; } = new();

    public DashboardViewModel(ISensorService sensors, LocalizationService loc)
    {
        _sensors = sensors;
        _loc = loc;
        _sensors.SnapshotUpdated += OnSnapshot;
        _loc.LanguageChanged += OnLanguageChanged;
        if (_sensors.Latest is { } latest) Apply(latest);
        RefreshDrives();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RefreshDrives();

    private void OnSnapshot(object? sender, HardwareSnapshot snapshot) =>
        Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(snapshot)));

    private void Apply(HardwareSnapshot s)
    {
        var cpu = s.Hardware.FirstOrDefault(h => h.Kind == HardwareKind.Cpu);
        CpuName = cpu?.Name ?? "CPU";
        CpuTemperature = Format(Pick(cpu, SensorKind.Temperature, "Package", "Tctl", "Tdie", "Core Max", "Average"), "\u00B0C");
        var cpuLoad = Pick(cpu, SensorKind.Load, "CPU Total");
        CpuLoad = cpuLoad ?? 0;
        CpuLoadText = Format(cpuLoad, "%");

        // NOTE: with several GPUs (iGPU + dGPU) this takes the first one. Selection by user comes later.
        var gpu = s.Hardware.FirstOrDefault(h => h.Kind == HardwareKind.Gpu);
        GpuName = gpu?.Name ?? "GPU";
        GpuTemperature = Format(Pick(gpu, SensorKind.Temperature, "GPU Core", "Hot Spot"), "\u00B0C");
        var gpuLoad = Pick(gpu, SensorKind.Load, "GPU Core");
        GpuLoad = gpuLoad ?? 0;
        GpuLoadText = Format(gpuLoad, "%");

        var mem = s.Hardware.FirstOrDefault(h => h.Kind == HardwareKind.Memory);
        var memLoad = Pick(mem, SensorKind.Load, "Memory");
        MemoryLoad = memLoad ?? 0;
        MemoryText = Format(memLoad, "%");
        var memUsed = Pick(mem, SensorKind.Data, "Memory Used");
        MemoryUsedText = memUsed is null ? "--" : _loc.Format("Dashboard_MemoryUsed", memUsed.Value);

        // Same selection the display service uses, so the preview matches the real display.
        var sent = TelemetrySelector.Select(s).CpuTemperatureC;
        DisplayPreviewText = sent is double temperature
            ? Math.Round(temperature).ToString("0").PadLeft(3)
            : "   ";

        if (_tick++ % 10 == 0) RefreshDrives();
    }

    private static float? Pick(HardwareNode? node, SensorKind kind, params string[] preferred)
    {
        if (node is null) return null;
        var candidates = node.AllSensors().Where(x => x.Kind == kind && x.Value.HasValue).ToList();
        foreach (var name in preferred)
        {
            var match = candidates.FirstOrDefault(x => x.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match.Value;
        }
        return candidates.FirstOrDefault()?.Value;
    }

    private static string Format(float? value, string unit) => value is null ? "--" : $"{value:0.#} {unit}";

    private void RefreshDrives()
    {
        const double gb = 1024d * 1024 * 1024;
        var fresh = new List<DriveViewModel>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                var total = d.TotalSize / gb;
                var free = d.AvailableFreeSpace / gb;
                var used = total - free;
                var percent = total <= 0 ? 0 : used / total * 100;
                var summary = _loc.Format("Dashboard_DriveSummary", used, free, total);
                fresh.Add(new DriveViewModel(d.Name.TrimEnd('\\'), percent, summary));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        Drives.Clear();
        foreach (var d in fresh) Drives.Add(d);
    }

    public void Dispose()
    {
        _sensors.SnapshotUpdated -= OnSnapshot;
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}
