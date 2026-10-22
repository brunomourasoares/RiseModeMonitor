namespace RiseModeMonitor.Core.Services;

/// <summary>Talks to the water cooler block display (HID).</summary>
public interface IDisplayService
{
    bool IsConnected { get; }

    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}
