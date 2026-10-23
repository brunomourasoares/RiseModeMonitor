using RiseModeMonitor.Core.Models;

namespace RiseModeMonitor.Core.Services;

/// <summary>
/// Manual fan speed control for the channels exposed by the motherboard Super I/O (and GPU) through
/// LibreHardwareMonitor. It does NOT control the water cooler pump or radiator fans: no validated
/// protocol exists for those (see docs/display-protocol.md).
/// </summary>
public interface IFanControlService
{
    /// <summary>Lowest duty cycle the app will ever apply, to avoid stopping a fan by mistake.</summary>
    const float SafeMinimumPercent = 20f;

    /// <summary>Current controllable channels (empty when the hardware exposes none).</summary>
    IReadOnlyList<FanChannel> GetChannels();

    /// <summary>Overrides the automatic curve with a fixed duty cycle (clamped to the allowed range).</summary>
    bool SetManual(string channelId, float percent);

    /// <summary>Gives the channel back to the BIOS / firmware automatic control.</summary>
    bool SetAutomatic(string channelId);

    /// <summary>Returns every channel the app changed to automatic control.</summary>
    void RestoreAll();
}
