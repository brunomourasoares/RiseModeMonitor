namespace RiseModeMonitor.Core.Services;

/// <summary>Registers the app to start with Windows (elevated, without a UAC prompt).</summary>
public interface IAutostartService
{
    /// <summary>True when the startup task currently exists in the system.</summary>
    bool IsEnabled();

    /// <summary>Creates or removes the startup task. Returns false on failure.</summary>
    bool SetEnabled(bool enabled);
}
