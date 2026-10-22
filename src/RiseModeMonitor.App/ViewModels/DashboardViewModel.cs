using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.ViewModels;

public sealed record DriveViewModel(string Name, double TotalGb, double UsedGb, double FreeGb)
{
    public double UsedPercent => TotalGb <= 0 ? 0 : UsedGb / TotalGb * 100;
    public string Summary => $"{UsedGb:0.#} GB used \u00B7 {FreeGb:0.#} GB free of {TotalGb:0.#} GB";
}

public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly ISensorService _sensors;
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

    public ObservableCollection<DriveViewModel> Drives { get; } = new();

    public DashboardViewModel(ISensorService sensors)
    {
        _sensors = sensors;
        _sensors.SnapshotUpdated += OnSnapshot;
        if (_sensors.Latest is { } latest) Apply(latest);
        RefreshDrives();
    }

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
        MemoryUsedText = Format(Pick(mem, SensorKind.Data, "Memory Used"), "GB used");

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
                fresh.Add(new DriveViewModel(d.Name.TrimEnd('\\'), total, total - free, free));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        Drives.Clear();
        foreach (var d in fresh) Drives.Add(d);
    }

    public void Dispose() => _sensors.SnapshotUpdated -= OnSnapshot;
}
