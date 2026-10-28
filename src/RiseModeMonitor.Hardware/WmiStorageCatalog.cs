using System.Management;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.Hardware;

/// <summary>
/// Maps drive letters to physical disks through WMI (Win32_DiskDrive, Win32_DiskDriveToDiskPartition and
/// Win32_LogicalDiskToPartition). The result is cached because these queries are slow.
/// </summary>
public sealed class WmiStorageCatalog : IStorageCatalog
{
    private readonly Lazy<IReadOnlyList<StorageDisk>> _disks = new(Query);

    public IReadOnlyList<StorageDisk> GetDisks() => _disks.Value;

    private static IReadOnlyList<StorageDisk> Query()
    {
        var result = new List<StorageDisk>();

        try
        {
            using var diskSearcher = new ManagementObjectSearcher("SELECT DeviceID, Model, Index FROM Win32_DiskDrive");

            var disks = diskSearcher.Get()
                .Cast<ManagementObject>()
                .OrderBy(d => Convert.ToInt32(d["Index"] ?? 0))
                .ToList();

            foreach (var disk in disks)
            {
                var model = (disk["Model"] as string)?.Trim();
                var deviceId = disk["DeviceID"] as string;
                if (string.IsNullOrEmpty(deviceId)) continue;

                var letters = new List<string>();

                // WQL needs backslashes doubled inside quoted paths (\\.\PHYSICALDRIVE0).
                var diskPath = deviceId.Replace("\\", "\\\\");
                using var partitionSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{diskPath}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");

                foreach (var partition in partitionSearcher.Get().Cast<ManagementObject>())
                {
                    if (partition["DeviceID"] is not string partitionId) continue;

                    using var logicalSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");

                    foreach (var logical in logicalSearcher.Get().Cast<ManagementObject>())
                    {
                        if (logical["DeviceID"] is string letter) letters.Add(letter);
                    }
                }

                result.Add(new StorageDisk(string.IsNullOrEmpty(model) ? deviceId : model, letters));
            }
        }
        catch (Exception)
        {
            // WMI unavailable or blocked: callers fall back to plain drive letters.
        }

        return result;
    }
}
