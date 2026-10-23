namespace RiseModeMonitor.Core.Models;

/// <summary>Values shown on the water cooler display. Null means "not available".</summary>
public sealed record DisplayTelemetry(
    double? CpuTemperatureC,
    double? GpuTemperatureC,
    double? CpuLoadPercent,
    double? MemoryLoadPercent,
    double? MotherboardTemperatureC,
    double? GpuLoadPercent);

public enum DisplayStatus
{
    Sent,
    DeviceNotFound,
    InvalidTelemetry,
    IncompatibleReport,
    OpenFailed,
    Error
}
