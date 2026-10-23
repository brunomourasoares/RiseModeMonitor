using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.ViewModels;

/// <summary>One controllable fan channel (card in the Fans page).</summary>
public sealed partial class FanChannelViewModel : ObservableObject
{
    private readonly IFanControlService _fans;
    private bool _updating;

    public string Id { get; }

    public string Name { get; }

    public string HardwareName { get; }

    [ObservableProperty] private double _minPercent;
    [ObservableProperty] private double _maxPercent = 100;
    [ObservableProperty] private double _percent;
    [ObservableProperty] private bool _isManual;
    [ObservableProperty] private string _rpmText = "--";

    /// <summary>
    /// False for channels with nothing spinning on them (0 RPM while in automatic mode), e.g. an
    /// unused header. Channels under manual control always stay usable so they can be restored.
    /// </summary>
    [ObservableProperty] private bool _isConnected = true;

    public FanChannelViewModel(FanChannel channel, IFanControlService fans)
    {
        _fans = fans;
        Id = channel.Id;
        Name = channel.Name;
        HardwareName = channel.HardwareName;
        Update(channel);
    }

    partial void OnIsManualChanged(bool value)
    {
        if (_updating) return;

        // Starting manual mode keeps the speed the fan is running at right now.
        if (value) _fans.SetManual(Id, (float)Percent);
        else _fans.SetAutomatic(Id);
    }

    partial void OnPercentChanged(double value)
    {
        if (_updating || !IsManual) return;
        _fans.SetManual(Id, (float)value);
    }

    /// <summary>Refreshes live values from the hardware without triggering commands.</summary>
    public void Update(FanChannel channel)
    {
        _updating = true;
        try
        {
            MinPercent = channel.MinPercent;
            MaxPercent = channel.MaxPercent;
            RpmText = channel.Rpm is float rpm ? $"{rpm:0} RPM" : "--";
            IsManual = channel.IsManual;
            IsConnected = channel.IsManual || (channel.Rpm ?? 0) > 0;

            // In automatic mode the slider just mirrors what the BIOS is doing.
            if (!channel.IsManual && channel.CurrentPercent is float current)
                Percent = current;
        }
        finally
        {
            _updating = false;
        }
    }
}

/// <summary>Fans page: manual speed control for the channels the hardware exposes.</summary>
public sealed partial class FansViewModel : ObservableObject, IDisposable
{
    private readonly ISensorService _sensors;
    private readonly IFanControlService _fans;

    public ObservableCollection<FanChannelViewModel> Channels { get; } = new();

    [ObservableProperty] private bool _isEmpty = true;

    public FansViewModel(ISensorService sensors, IFanControlService fans)
    {
        _sensors = sensors;
        _fans = fans;
        _sensors.SnapshotUpdated += OnSnapshot;
        Refresh();
    }

    private void OnSnapshot(object? sender, HardwareSnapshot snapshot) =>
        Application.Current?.Dispatcher.BeginInvoke(new Action(Refresh));

    private void Refresh()
    {
        var channels = _fans.GetChannels();

        if (!Channels.Select(c => c.Id).SequenceEqual(channels.Select(c => c.Id)))
        {
            Channels.Clear();
            foreach (var channel in channels)
                Channels.Add(new FanChannelViewModel(channel, _fans));
        }
        else
        {
            for (var i = 0; i < channels.Count; i++)
                Channels[i].Update(channels[i]);
        }

        IsEmpty = Channels.Count == 0;
    }

    [RelayCommand]
    private void RestoreAll()
    {
        _fans.RestoreAll();
        Refresh();
    }

    public void Dispose() => _sensors.SnapshotUpdated -= OnSnapshot;
}
