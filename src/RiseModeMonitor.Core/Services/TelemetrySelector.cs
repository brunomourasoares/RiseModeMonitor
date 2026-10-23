using RiseModeMonitor.Core.Models;

namespace RiseModeMonitor.Core.Services;

/// <summary>
/// Picks the sensors that represent "the CPU temperature", "the GPU load" and so on from a full
/// hardware snapshot. Rules ported from the previous RMMonitor app.
/// </summary>
public static class TelemetrySelector
{
    public static DisplayTelemetry Select(HardwareSnapshot snapshot)
    {
        var nodes = Flatten(snapshot.Hardware).ToList();

        return new DisplayTelemetry(
            CpuTemperatureC: Best(nodes, SensorKind.Temperature, CpuTemperaturePriority, IsValidTemperature),
            GpuTemperatureC: Best(nodes, SensorKind.Temperature, GpuTemperaturePriority, IsValidTemperature, HardwareKind.Gpu),
            CpuLoadPercent: Best(nodes, SensorKind.Load, CpuLoadPriority, IsValidPercent, HardwareKind.Cpu),
            MemoryLoadPercent: MemoryLoad(nodes),
            MotherboardTemperatureC: null, // No validated mapping for the board temperature byte yet.
            GpuLoadPercent: Best(nodes, SensorKind.Load, GpuLoadPriority, IsValidPercent, HardwareKind.Gpu));
    }

    private static IEnumerable<HardwareNode> Flatten(IEnumerable<HardwareNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }

    private static double? Best(
        IEnumerable<HardwareNode> nodes,
        SensorKind sensorKind,
        Func<HardwareKind, string, int> priority,
        Func<double, bool> isValid,
        HardwareKind? onlyKind = null)
    {
        double? best = null;
        var bestPriority = 0; // Priorities <= 0 mean "not a candidate".

        foreach (var node in nodes)
        {
            if (onlyKind is { } kind && node.Kind != kind) continue;

            foreach (var sensor in node.Sensors)
            {
                if (sensor.Kind != sensorKind || sensor.Value is not float value) continue;
                if (!isValid(value)) continue;

                var p = priority(node.Kind, sensor.Name);
                if (p > bestPriority)
                {
                    bestPriority = p;
                    best = value;
                }
            }
        }

        return best;
    }

    private static double? MemoryLoad(IEnumerable<HardwareNode> nodes)
    {
        // "Total Memory" has the physical RAM load; "Virtual Memory" is the commit charge.
        var memory = nodes.FirstOrDefault(n =>
            n.Kind == HardwareKind.Memory && !n.Name.Contains("virtual", StringComparison.OrdinalIgnoreCase));

        var load = memory?.Sensors.FirstOrDefault(s =>
            s.Kind == SensorKind.Load && s.Value.HasValue && s.Name.Contains("memory", StringComparison.OrdinalIgnoreCase));

        return load?.Value is float v && IsValidPercent(v) ? v : null;
    }

    private static bool IsValidTemperature(double value) => double.IsFinite(value) && value is >= 0 and <= 150;

    private static bool IsValidPercent(double value) => double.IsFinite(value) && value is >= 0 and <= 100;

    private static int CpuTemperaturePriority(HardwareKind kind, string name)
    {
        if (kind == HardwareKind.Cpu)
        {
            if (Has(name, "package")) return 100;
            if (Has(name, "tdie")) return 95;
            if (Has(name, "tctl")) return 90;
            if (Has(name, "core max")) return 85;
            if (Has(name, "average")) return 80;
            if (Has(name, "core")) return 70;
            return 60;
        }

        // Some boards only expose a CPU-labelled sensor under Motherboard/Super I/O.
        // Generic motherboard or VRM temperatures must not be mistaken for the CPU.
        if (kind == HardwareKind.Motherboard)
        {
            if (!Has(name, "cpu") && !Has(name, "processor")) return -1;
            if (Has(name, "package")) return 55;
            if (Has(name, "core")) return 50;
            if (Has(name, "socket")) return 45;
            return 40;
        }

        return -1;
    }

    private static int CpuLoadPriority(HardwareKind kind, string name)
    {
        if (Has(name, "total")) return 100;
        if (Has(name, "average")) return 70;
        // "core" is tested before "cpu": "CPU Core #1" is a per-core value, not the aggregate.
        if (Has(name, "core")) return 30;
        if (Has(name, "cpu")) return 80;
        return -1;
    }

    private static int GpuTemperaturePriority(HardwareKind kind, string name)
    {
        if (Has(name, "gpu core") || Has(name, "core temperature")) return 100;
        if (Has(name, "gpu temperature") || name.Equals("temperature", StringComparison.OrdinalIgnoreCase)) return 90;
        if (Has(name, "core")) return 80;
        if (Has(name, "junction") || Has(name, "hot spot") || Has(name, "hotspot")) return 50;
        if (Has(name, "memory")) return 20;
        return 10;
    }

    private static int GpuLoadPriority(HardwareKind kind, string name)
    {
        if (Has(name, "total")) return 100;
        if (Has(name, "core")) return 80;
        if (Has(name, "gpu")) return 70;
        return 10;
    }

    private static bool Has(string name, string part) => name.Contains(part, StringComparison.OrdinalIgnoreCase);
}
