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
			using var diskSearcher = new ManagementObjectSearcher(
				"SELECT DeviceID, Model, Index FROM Win32_DiskDrive");

			using var diskResults = diskSearcher.Get();

			var disks = new List<(int Index, string DeviceId, string? Model)>();

			foreach (ManagementObject disk in diskResults)
			{
				using (disk)
				{
					if (disk["DeviceID"] is not string deviceId || deviceId.Length == 0)
						continue;

					var index = Convert.ToInt32(disk["Index"] ?? 0);
					var model = (disk["Model"] as string)?.Trim();

					disks.Add((index, deviceId, model));
				}
			}

			foreach (var (index, deviceId, model) in disks.OrderBy(d => d.Index))
			{
				var letters = new List<string>();

				try
				{
					// Busca as partições diretamente pelo índice do disco físico.
					using var partitionSearcher = new ManagementObjectSearcher(
						$"SELECT DeviceID FROM Win32_DiskPartition WHERE DiskIndex = {index}");

					using var partitions = partitionSearcher.Get();

					foreach (ManagementObject partition in partitions)
					{
						using (partition)
						{
							if (partition["DeviceID"] is not string partitionId)
								continue;

							// Associa a partição às unidades lógicas.
							using var logicalSearcher = new ManagementObjectSearcher(
								$"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} " +
								"WHERE AssocClass = Win32_LogicalDiskToPartition");

							using var logicals = logicalSearcher.Get();

							foreach (ManagementObject logical in logicals)
							{
								using (logical)
								{
									if (logical["DeviceID"] is string letter)
									{
										letters.Add(letter);
									}
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Erro ao consultar partições do disco {index}: {ex}");
				}

				result.Add(new StorageDisk(
					string.IsNullOrWhiteSpace(model) ? deviceId : model,
					letters));
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"WMI storage query failed: {ex}");
		}

		return result;
	}
}
