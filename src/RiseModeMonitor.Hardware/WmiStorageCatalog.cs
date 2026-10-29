using System.Diagnostics;
using System.Management;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.Hardware;

/// <summary>
/// Maps drive letters to physical disks through WMI (Win32_DiskDrive, Win32_DiskDriveToDiskPartition and
/// Win32_LogicalDiskToPartition). The result is cached because these queries are slow, and refreshed
/// after a while so disks plugged in later appear (and an empty result from a transient failure heals).
/// </summary>
public sealed class WmiStorageCatalog : IStorageCatalog
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan EmptyRetry = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private IReadOnlyList<StorageDisk>? _cache;
    private DateTime _cachedAtUtc;

    public IReadOnlyList<StorageDisk> GetDisks()
    {
        lock (_gate)
        {
            var maxAge = _cache is { Count: > 0 } ? CacheLifetime : EmptyRetry;
            if (_cache is null || DateTime.UtcNow - _cachedAtUtc > maxAge)
            {
                _cache = Query();
                _cachedAtUtc = DateTime.UtcNow;
            }

            return _cache;
        }
    }

    private static IReadOnlyList<StorageDisk> Query()
    {
        var result = new List<StorageDisk>();

        try
        {
            using var diskSearcher = new ManagementObjectSearcher("SELECT DeviceID, Model, Index FROM Win32_DiskDrive");
            using var diskResults = diskSearcher.Get();

            var disks = new List<(int Index, string DeviceId, string? Model)>();
            foreach (ManagementObject disk in diskResults)
            {
                using (disk)
                {
                    if (disk["DeviceID"] is not string deviceId || deviceId.Length == 0) continue;
                    disks.Add((Convert.ToInt32(disk["Index"] ?? 0), deviceId, (disk["Model"] as string)?.Trim()));
                }
            }

            foreach (var (_, deviceId, model) in disks.OrderBy(d => d.Index))
            {
                var letters = new List<string>();

                // WQL needs backslashes doubled inside quoted paths (\\.\PHYSICALDRIVE0).
                var diskPath = deviceId.Replace("\\", "\\\\");
                using var partitionSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{diskPath}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");
                using var partitions = partitionSearcher.Get();

                foreach (ManagementObject partition in partitions)
                {
                    using (partition)
                    {
                        if (partition["DeviceID"] is not string partitionId) continue;

                        using var logicalSearcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");
                        using var logicals = logicalSearcher.Get();

                        foreach (ManagementObject logical in logicals)
                        {
                            using (logical)
                            {
                                if (logical["DeviceID"] is string letter) letters.Add(letter);
                            }
                        }
                    }
                }

                result.Add(new StorageDisk(string.IsNullOrEmpty(model) ? deviceId : model, letters));
            }
        }
        catch (Exception ex)
        {
            // WMI unavailable or blocked: callers fall back to plain drive letters.
            Debug.WriteLine($"WMI storage query failed: {ex}");
        }

        return result;
    }
}
