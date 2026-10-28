using RiseModeMonitor.Core.Models;

namespace RiseModeMonitor.Core.Services;

/// <summary>Tells which physical storage device each drive letter belongs to.</summary>
public interface IStorageCatalog
{
    /// <summary>
    /// Physical disks with their volumes. Computed once and cached (the query is slow), so the first
    /// call can take a moment. Empty when Windows does not answer.
    /// </summary>
    IReadOnlyList<StorageDisk> GetDisks();
}
