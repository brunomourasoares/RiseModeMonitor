using RiseModeMonitor.Core.Models;

namespace RiseModeMonitor.Core.Services;

public interface ISettingsService
{
    AppSettings Current { get; }

    void Save();
}
