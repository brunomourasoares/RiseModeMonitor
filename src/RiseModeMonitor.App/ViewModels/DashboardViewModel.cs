using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Localization;

namespace RiseModeMonitor.ViewModels;

/// <summary>Volume data built in the background (no UI objects): one line and its usage percentage.</summary>
internal sealed record VolumeData(string Letter, string Line, double UsedPercent);

/// <summary>Disk data built in the background. <paramref name="Key"/> identifies the card between refreshes.</summary>
internal sealed record DiskData(string Key, string Model, IReadOnlyList<VolumeData> Volumes);

/// <summary>One volume line, e.g. "C:  209,5 GB / 232,3 GB - 22,8 GB free", with its usage bar.</summary>
public sealed partial class VolumeViewModel : ObservableObject
{
    public VolumeViewModel(string letter, string line, double usedPercent)
    {
        Letter = letter;
        _line = line;
        _usedPercent = usedPercent;
    }

    public string Letter { get; }

    [ObservableProperty] private string _line;
    [ObservableProperty] private double _usedPercent;
}

/// <summary>
/// A physical storage device (card) with its volumes. It is updated in place on every refresh, so the
/// cards keep their position and the page does not flicker or lose its scroll position.
/// </summary>
public sealed partial class StorageDiskViewModel : ObservableObject
{
    public StorageDiskViewModel(string key, string model)
    {
        Key = key;
        _model = model;
    }

    public string Key { get; }

    [ObservableProperty] private string _model;

    public ObservableCollection<VolumeViewModel> Volumes { get; } = new();

    internal void Update(DiskData data)
    {
        Model = data.Model;

        for (var i = Volumes.Count - 1; i >= 0; i--)
        {
            if (!data.Volumes.Any(v => v.Letter == Volumes[i].Letter)) Volumes.RemoveAt(i);
        }

        for (var i = 0; i < data.Volumes.Count; i++)
        {
            var item = data.Volumes[i];
            var existing = Volumes.FirstOrDefault(v => v.Letter == item.Letter);

            if (existing is null)
            {
                Volumes.Insert(i, new VolumeViewModel(item.Letter, item.Line, item.UsedPercent));
                continue;
            }

            var current = Volumes.IndexOf(existing);
            if (current != i) Volumes.Move(current, i);

            existing.Line = item.Line;
            existing.UsedPercent = item.UsedPercent;
        }
    }
}

public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly ISensorService _sensors;
    private readonly IStorageCatalog _catalog;
    private readonly LocalizationService _loc;
    private IReadOnlyList<string> _storageNames = Array.Empty<string>();
    private bool _catalogReady;
    private int _tick;
    private int _refreshing;
    private volatile bool _refreshAgain;
    private volatile bool _disposed;

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

    public ObservableCollection<StorageDiskViewModel> Disks { get; } = new();

    public DashboardViewModel(ISensorService sensors, IStorageCatalog catalog, LocalizationService loc)
    {
        _sensors = sensors;
        _catalog = catalog;
        _loc = loc;
        _sensors.SnapshotUpdated += OnSnapshot;
        _loc.LanguageChanged += OnLanguageChanged;
        if (_sensors.Latest is { } latest) Apply(latest);
        _ = LoadCatalogAsync();
    }

    /// <summary>The WMI query is slow: run it in the background and fill the cards when it is done.</summary>
    private async Task LoadCatalogAsync()
    {
        await Task.Run(() => _catalog.GetDisks());
        _catalogReady = true;
        _ = RefreshDisksAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => _ = RefreshDisksAsync();

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

        // Clean model names come from the sensor library (e.g. "Samsung SSD 840 EVO 250GB").
        var names = s.Hardware.Where(h => h.Kind == HardwareKind.Storage).Select(h => h.Name).ToList();
        var namesChanged = !names.SequenceEqual(_storageNames);
        _storageNames = names;

        // Refresh every 10 snapshots, and right away when the sensor library names the disks (first
        // snapshot or a new disk), so the cards never stay with the generic Windows name for long.
        var due = _tick++ % 10 == 0;
        if (due || namesChanged) _ = RefreshDisksAsync();
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

    /// <summary>
    /// Disk and volume queries can block (slow or sleeping drives), so they run in the background and
    /// only the result is applied on the UI thread. A request that arrives while a refresh is running
    /// is not lost: it runs again as soon as the current one finishes.
    /// </summary>
    private async Task RefreshDisksAsync()
    {
        if (!_catalogReady) return;

        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
        {
            _refreshAgain = true;
            return;
        }

        try
        {
            do
            {
                _refreshAgain = false;

                var names = _storageNames;
                var data = await Task.Run(() => BuildDisks(names));
                if (_disposed) return;

                ApplyDisks(data);
            }
            while (_refreshAgain);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Storage refresh failed: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    /// <summary>Updates the cards in place: existing cards keep their identity, only values change.</summary>
    private void ApplyDisks(IReadOnlyList<DiskData> data)
    {
        for (var i = Disks.Count - 1; i >= 0; i--)
        {
            if (!data.Any(d => d.Key == Disks[i].Key)) Disks.RemoveAt(i);
        }

        for (var i = 0; i < data.Count; i++)
        {
            var item = data[i];
            var existing = Disks.FirstOrDefault(d => d.Key == item.Key);

            if (existing is null)
            {
                var card = new StorageDiskViewModel(item.Key, item.Model);
                Disks.Insert(i, card);
                card.Update(item);
                continue;
            }

            var current = Disks.IndexOf(existing);
            if (current != i) Disks.Move(current, i);

            existing.Update(item);
        }
    }

    private List<DiskData> BuildDisks(IReadOnlyList<string> sensorNames)
    {
        var disks = _catalog.GetDisks();
        var fresh = new List<DiskData>();

        // Drive letters that Windows attributed to a physical disk.
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var disk in disks)
        {
            foreach (var letter in disk.VolumeLetters) covered.Add(letter);

            var volumes = disk.VolumeLetters.Select(BuildVolume).OfType<VolumeData>().ToList();
            if (volumes.Count == 0) continue; // nothing mounted (e.g. recovery-only disk): no empty card

            fresh.Add(new DiskData(string.Join("|", disk.VolumeLetters), DisplayModel(disk.Model, sensorNames), volumes));
        }

        // Fixed drives Windows could not map to a disk (Storage Spaces, RAID, VHD...) or an empty
        // catalog: show each of them on its own so no drive disappears from the dashboard.
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            var letter = drive.Name.TrimEnd('\\');
            if (covered.Contains(letter)) continue;

            if (BuildVolume(letter) is { } volume)
                fresh.Add(new DiskData(letter, letter, new[] { volume }));
        }

        return fresh;
    }

    private VolumeData? BuildVolume(string letter)
    {
        const double gb = 1024d * 1024 * 1024;

        try
        {
            var drive = new DriveInfo(letter);
            if (!drive.IsReady) return null;

            var total = drive.TotalSize / gb;
            var free = drive.AvailableFreeSpace / gb;
            var used = total - free;
            var percent = total <= 0 ? 0 : used / total * 100;

            return new VolumeData(letter, _loc.Format("Dashboard_VolumeLine", letter, used, total, free), percent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Prefers the name the sensor library uses for the same disk; otherwise strips the generic suffix
    /// Windows adds ("... ATA Device", "... SCSI Disk Device").
    /// </summary>
    private static string DisplayModel(string windowsModel, IReadOnlyList<string> sensorNames)
    {
        var match = sensorNames.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n) && windowsModel.Contains(n, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        var cleaned = Regex.Replace(windowsModel, @"\s+(ATA|SCSI|NVMe|USB)(\s+\w+)*\s+Device$", string.Empty, RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    public void Dispose()
    {
        _disposed = true;
        _sensors.SnapshotUpdated -= OnSnapshot;
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}
