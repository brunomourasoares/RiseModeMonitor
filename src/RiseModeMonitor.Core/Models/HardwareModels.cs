namespace RiseModeMonitor.Core.Models;

public enum HardwareKind { Cpu, Gpu, Memory, Motherboard, Storage, Network, Cooler, Other }

public enum SensorKind { Temperature, Load, Clock, Power, Voltage, Fan, Control, Data, SmallData, Throughput, Other }

public sealed record SensorReading(
    string Id,
    string Name,
    SensorKind Kind,
    float? Value,
    float? Min,
    float? Max,
    string Unit);

public sealed record HardwareNode(
    string Id,
    string Name,
    HardwareKind Kind,
    IReadOnlyList<SensorReading> Sensors,
    IReadOnlyList<HardwareNode> Children)
{
    /// <summary>All sensors of this node and of its sub-hardware, recursively.</summary>
    public IEnumerable<SensorReading> AllSensors() =>
        Sensors.Concat(Children.SelectMany(c => c.AllSensors()));
}

public sealed record HardwareSnapshot(
    DateTimeOffset Timestamp,
    IReadOnlyList<HardwareNode> Hardware);
