namespace RiseModeMonitor.Core.Models;

/// <summary>A physical storage device and the drive letters of its volumes.</summary>
/// <param name="Model">Model name reported by Windows, e.g. "Samsung SSD 840 EVO 250GB ATA Device".</param>
/// <param name="VolumeLetters">Mounted volumes living on this device, e.g. "C:".</param>
public sealed record StorageDisk(string Model, IReadOnlyList<string> VolumeLetters);
