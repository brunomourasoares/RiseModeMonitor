using RiseModeMonitor.Core.Models;

namespace RiseModeMonitor.Core.Services;

/// <summary>Sends telemetry to the water cooler block display (HID).</summary>
public interface IDisplayService : IDisposable
{
    /// <summary>
    /// Encodes and writes one telemetry packet. A successful write only means the OS accepted it,
    /// not that the display rendered it.
    /// </summary>
    DisplayStatus Send(DisplayTelemetry telemetry);
}
