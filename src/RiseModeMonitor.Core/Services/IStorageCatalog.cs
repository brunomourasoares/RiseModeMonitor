using RiseModeMonitor.Core.Models;

namespace RiseModeMonitor.Core.Services;

/// <summary>Tells which physical storage device each drive letter belongs to.</summary>
public interface IStorageCatalog
{
    /// <summary>
    /// Physical disks with their volumes. The result is cached for a couple of minutes (the query is
    /// slow) and refreshed afterwards, so a disk plugged in later shows up. The call can block while
    /// the query runs: call it from a background thread. Empty when Windows does not answer.
    /// </summary>
    IReadOnlyList<StorageDisk> GetDisks();
}
