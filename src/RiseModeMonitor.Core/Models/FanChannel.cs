namespace RiseModeMonitor.Core.Models;

/// <summary>A fan (or pump) channel whose speed can be controlled by software.</summary>
/// <param name="Id">Stable identifier of the control sensor.</param>
/// <param name="Name">Sensor name, e.g. "Fan #1".</param>
/// <param name="HardwareName">Chip or device that owns the channel.</param>
/// <param name="CurrentPercent">Current duty cycle, when known.</param>
/// <param name="Rpm">Measured speed of the matching fan sensor, when available.</param>
/// <param name="IsManual">True while the app is overriding the automatic (BIOS) curve.</param>
/// <param name="MinPercent">Lowest value the app allows.</param>
/// <param name="MaxPercent">Highest value the app allows.</param>
public sealed record FanChannel(
    string Id,
    string Name,
    string HardwareName,
    float? CurrentPercent,
    float? Rpm,
    bool IsManual,
    float MinPercent,
    float MaxPercent);
