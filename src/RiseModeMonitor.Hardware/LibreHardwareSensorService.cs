using LibreHardwareMonitor.Hardware;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.Hardware;

/// <summary>
/// ISensorService backed by LibreHardwareMonitorLib. Requires administrator rights
/// (and the PawnIO driver) to expose most sensors.
/// </summary>
public sealed class LibreHardwareSensorService : ISensorService, IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true,
        IsStorageEnabled = true,
        IsNetworkEnabled = true,
        IsControllerEnabled = true
    };

    private readonly object _gate = new();
    private Timer? _timer;
    private bool _opened;
    private int _ticking;

    public event EventHandler<HardwareSnapshot>? SnapshotUpdated;

    public HardwareSnapshot? Latest { get; private set; }

    public void Start(TimeSpan interval)
    {
        lock (_gate)
        {
            if (_timer is not null) return;
            if (!_opened)
            {
                _computer.Open();
                _opened = true;
            }
            _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, interval);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Tick()
    {
        // Skip the tick if the previous one is still running.
        if (Interlocked.Exchange(ref _ticking, 1) == 1) return;
        try
        {
            HardwareSnapshot snapshot;
            lock (_gate)
            {
                if (_timer is null) return;
                var nodes = _computer.Hardware.Select(UpdateNode).ToList();
                snapshot = new HardwareSnapshot(DateTimeOffset.Now, nodes);
                Latest = snapshot;
            }
            SnapshotUpdated?.Invoke(this, snapshot);
        }
        catch
        {
            // TODO: add logging (Microsoft.Extensions.Logging / Serilog).
        }
        finally
        {
            Interlocked.Exchange(ref _ticking, 0);
        }
    }

    private static HardwareNode UpdateNode(IHardware hardware)
    {
        hardware.Update();
        var children = hardware.SubHardware.Select(UpdateNode).ToList();
        var sensors = hardware.Sensors.Select(MapSensor).ToList();
        return new HardwareNode(hardware.Identifier.ToString(), hardware.Name, MapHardware(hardware.HardwareType), sensors, children);
    }

    private static SensorReading MapSensor(ISensor sensor)
    {
        var kind = MapSensorKind(sensor.SensorType);
        return new SensorReading(sensor.Identifier.ToString(), sensor.Name, kind, sensor.Value, sensor.Min, sensor.Max, UnitFor(kind, sensor.SensorType));
    }

    private static HardwareKind MapHardware(HardwareType type) => type switch
    {
        HardwareType.Cpu => HardwareKind.Cpu,
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => HardwareKind.Gpu,
        HardwareType.Memory => HardwareKind.Memory,
        HardwareType.Motherboard or HardwareType.SuperIO or HardwareType.EmbeddedController => HardwareKind.Motherboard,
        HardwareType.Storage => HardwareKind.Storage,
        HardwareType.Network => HardwareKind.Network,
        HardwareType.Cooler => HardwareKind.Cooler,
        _ => HardwareKind.Other
    };

    private static SensorKind MapSensorKind(SensorType type) => type switch
    {
        SensorType.Temperature => SensorKind.Temperature,
        SensorType.Load => SensorKind.Load,
        SensorType.Clock => SensorKind.Clock,
        SensorType.Power => SensorKind.Power,
        SensorType.Voltage => SensorKind.Voltage,
        SensorType.Fan => SensorKind.Fan,
        SensorType.Control => SensorKind.Control,
        SensorType.Data => SensorKind.Data,
        SensorType.SmallData => SensorKind.SmallData,
        SensorType.Throughput => SensorKind.Throughput,
        _ => SensorKind.Other
    };

    private static string UnitFor(SensorKind kind, SensorType raw) => kind switch
    {
        SensorKind.Temperature => "\u00B0C",
        SensorKind.Load or SensorKind.Control => "%",
        SensorKind.Clock => "MHz",
        SensorKind.Power => "W",
        SensorKind.Voltage => "V",
        SensorKind.Fan => "RPM",
        SensorKind.Data => "GB",
        SensorKind.SmallData => "MB",
        SensorKind.Throughput => "B/s",
        _ => raw == SensorType.Level ? "%" : string.Empty
    };

    public void Dispose()
    {
        Stop();
        lock (_gate)
        {
            if (_opened)
            {
                _computer.Close();
                _opened = false;
            }
        }
    }
}
