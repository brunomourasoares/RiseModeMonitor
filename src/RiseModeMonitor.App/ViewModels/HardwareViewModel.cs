using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.ViewModels;

public sealed record HardwareItemViewModel(string Id, string Name, string KindLabel, Thickness Indent);

public sealed record SensorGroupViewModel(string Title, IReadOnlyList<SensorItemViewModel> Sensors);

/// <summary>One sensor row. Values are updated in place so the UI does not flicker.</summary>
public sealed partial class SensorItemViewModel : ObservableObject
{
    private readonly string _unit;

    public string Name { get; }

    [ObservableProperty] private string _value = "--";
    [ObservableProperty] private string _min = "--";
    [ObservableProperty] private string _max = "--";

    public SensorItemViewModel(SensorReading reading)
    {
        Name = reading.Name;
        _unit = reading.Unit;
        Update(reading);
    }

    public void Update(SensorReading reading)
    {
        Value = Format(reading.Value);
        Min = Format(reading.Min);
        Max = Format(reading.Max);
    }

    private string Format(float? v) => v is null ? "--" : $"{v:0.##} {_unit}".TrimEnd();
}

/// <summary>Master-detail hardware page: hardware list on the left, grouped sensors on the right.</summary>
public sealed partial class HardwareViewModel : ObservableObject, IDisposable
{
    private sealed record FlatHardware(string Id, string Name, HardwareKind Kind, int Depth, IReadOnlyList<SensorReading> Sensors);

    private readonly ISensorService _sensors;
    private readonly Dictionary<string, SensorItemViewModel> _sensorVms = new();
    private readonly List<string> _displayedIds = new();
    private IReadOnlyList<FlatHardware> _flat = Array.Empty<FlatHardware>();

    public ObservableCollection<HardwareItemViewModel> Hardware { get; } = new();

    public ObservableCollection<SensorGroupViewModel> Groups { get; } = new();

    [ObservableProperty] private HardwareItemViewModel? _selected;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isEmpty = true;

    public HardwareViewModel(ISensorService sensors)
    {
        _sensors = sensors;
        _sensors.SnapshotUpdated += OnSnapshot;
        if (_sensors.Latest is { } latest) Apply(latest);
    }

    partial void OnSelectedChanged(HardwareItemViewModel? value) => Refresh();

    partial void OnSearchTextChanged(string value) => Refresh();

    private void OnSnapshot(object? sender, HardwareSnapshot snapshot) =>
        Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(snapshot)));

    private void Apply(HardwareSnapshot snapshot)
    {
        _flat = Flatten(snapshot.Hardware, 0).ToList();

        if (!Hardware.Select(h => h.Id).SequenceEqual(_flat.Select(f => f.Id)))
        {
            var selectedId = Selected?.Id;
            Selected = null;
            Hardware.Clear();
            foreach (var f in _flat)
                Hardware.Add(new HardwareItemViewModel(f.Id, f.Name, f.Kind.ToString(), new Thickness(f.Depth * 14, 0, 0, 0)));
            Selected = Hardware.FirstOrDefault(h => h.Id == selectedId) ?? Hardware.FirstOrDefault();
        }

        Refresh();
    }

    private static IEnumerable<FlatHardware> Flatten(IEnumerable<HardwareNode> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            yield return new FlatHardware(node.Id, node.Name, node.Kind, depth, node.Sensors);
            foreach (var child in Flatten(node.Children, depth + 1))
                yield return child;
        }
    }

    private void Refresh()
    {
        var current = Selected is null ? null : _flat.FirstOrDefault(f => f.Id == Selected.Id);
        var visible = current is null
            ? new List<SensorReading>()
            : current.Sensors.Where(Matches).ToList();

        var ids = visible.Select(r => r.Id).ToList();
        foreach (var reading in visible)
        {
            if (_sensorVms.TryGetValue(reading.Id, out var vm)) vm.Update(reading);
            else _sensorVms[reading.Id] = new SensorItemViewModel(reading);
        }

        // Rebuild the groups only when the set of visible sensors changed.
        if (ids.SequenceEqual(_displayedIds)) return;

        _displayedIds.Clear();
        _displayedIds.AddRange(ids);

        Groups.Clear();
        foreach (var group in visible.GroupBy(r => r.Kind).OrderBy(g => g.Key))
            Groups.Add(new SensorGroupViewModel(Label(group.Key), group.Select(r => _sensorVms[r.Id]).ToList()));

        IsEmpty = Groups.Count == 0;
    }

    private bool Matches(SensorReading reading) =>
        string.IsNullOrWhiteSpace(SearchText) ||
        reading.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Label(SensorKind kind) => kind switch
    {
        SensorKind.Temperature => "Temperatures",
        SensorKind.Load => "Load",
        SensorKind.Clock => "Clocks",
        SensorKind.Power => "Power",
        SensorKind.Voltage => "Voltages",
        SensorKind.Fan => "Fans",
        SensorKind.Control => "Controls",
        SensorKind.Data => "Data",
        SensorKind.SmallData => "Data (small)",
        SensorKind.Throughput => "Throughput",
        _ => "Other"
    };

    public void Dispose() => _sensors.SnapshotUpdated -= OnSnapshot;
}
