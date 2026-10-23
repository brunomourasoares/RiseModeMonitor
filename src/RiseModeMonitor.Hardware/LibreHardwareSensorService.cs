using LibreHardwareMonitor.Hardware;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.Hardware;

/// <summary>
/// ISensorService and IFanControlService backed by LibreHardwareMonitorLib. Requires administrator
/// rights (and the PawnIO driver) to expose most sensors and to control fans.
/// </summary>
public sealed class LibreHardwareSensorService : ISensorService, IFanControlService, IDisposable
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
    private readonly HashSet<string> _manualChannels = new();
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

    // ---- Fan control -------------------------------------------------------------------------

    public IReadOnlyList<FanChannel> GetChannels()
    {
        lock (_gate)
        {
            if (!_opened) return Array.Empty<FanChannel>();

            var channels = new List<FanChannel>();
            foreach (var (hardware, sensor) in _computer.Hardware.SelectMany(ControlSensorsOf))
            {
                var control = sensor.Control!;
                var (min, max) = AllowedRange(control);
                var rpm = hardware.Sensors
                    .FirstOrDefault(s => s.SensorType == SensorType.Fan && s.Name == sensor.Name)?.Value;

                channels.Add(new FanChannel(
                    sensor.Identifier.ToString(),
                    sensor.Name,
                    hardware.Name,
                    sensor.Value,
                    rpm,
                    control.ControlMode == ControlMode.Software,
                    min,
                    max));
            }
            return channels;
        }
    }

    public bool SetManual(string channelId, float percent)
    {
        lock (_gate)
        {
            if (FindControl(channelId) is not { } control) return false;

            try
            {
                var (min, max) = AllowedRange(control);
                control.SetSoftware(Math.Clamp(percent, min, max));
                _manualChannels.Add(channelId);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public bool SetAutomatic(string channelId)
    {
        lock (_gate)
        {
            if (FindControl(channelId) is not { } control) return false;

            try
            {
                control.SetDefault();
                _manualChannels.Remove(channelId);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public void RestoreAll()
    {
        string[] ids;
        lock (_gate) ids = _manualChannels.ToArray();

        foreach (var id in ids)
            SetAutomatic(id);
    }

    private IControl? FindControl(string channelId) =>
        _computer.Hardware
            .SelectMany(ControlSensorsOf)
            .FirstOrDefault(x => x.Sensor.Identifier.ToString() == channelId)
            .Sensor?.Control;

    private static IEnumerable<(IHardware Hardware, ISensor Sensor)> ControlSensorsOf(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Control && sensor.Control is not null)
                yield return (hardware, sensor);
        }

        foreach (var sub in hardware.SubHardware)
        {
            foreach (var item in ControlSensorsOf(sub))
                yield return item;
        }
    }

    /// <summary>Hardware limits intersected with the app's safety floor.</summary>
    private static (float Min, float Max) AllowedRange(IControl control)
    {
        var min = Math.Max(control.MinSoftwareValue, IFanControlService.SafeMinimumPercent);
        var max = Math.Min(control.MaxSoftwareValue, 100f);
        return (min, Math.Max(min, max));
    }

    // ---- Sensor mapping ----------------------------------------------------------------------

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
        // Never leave a fan stuck at a manual speed when the app exits.
        RestoreAll();
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
