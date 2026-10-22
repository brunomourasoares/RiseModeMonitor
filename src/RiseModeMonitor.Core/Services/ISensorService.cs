using RiseModeMonitor.Core.Models;

namespace RiseModeMonitor.Core.Services;

/// <summary>Reads hardware sensors periodically and publishes snapshots.</summary>
public interface ISensorService
{
    /// <summary>Raised on a background thread after every polling cycle.</summary>
    event EventHandler<HardwareSnapshot>? SnapshotUpdated;

    HardwareSnapshot? Latest { get; }

    void Start(TimeSpan interval);

    void Stop();
}
