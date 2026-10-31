using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Localization;

namespace RiseModeMonitor.ViewModels;

/// <summary>One controllable fan channel (card in the Fans page).</summary>
public sealed partial class FanChannelViewModel : ObservableObject
{
    private readonly IFanControlService _fans;
    private readonly ISettingsService _settings;
    private bool _updating;

    /// <summary>Consecutive 0 RPM readings (automatic mode) after which a fan that used to spin is reported as stopped.</summary>
    private const int IdleReadingsToWarn = 3;

    private int _idleReadings;

    // What the hardware reports right now; used as the starting point when manual mode is switched on.
    private double? _reportedPercent;

    public string Id { get; }

    /// <summary>The generic name reported by the hardware library, e.g. "Fan #1".</summary>
    public string Name { get; }

    public string HardwareName { get; }

    /// <summary>Name shown on the card. Editable; saved per channel. Empty input restores <see cref="Name"/>.</summary>
    [ObservableProperty] private string _displayName;

    [ObservableProperty] private double _minPercent;
    [ObservableProperty] private double _maxPercent = 100;
    [ObservableProperty] private double _percent;
    [ObservableProperty] private bool _isManual;
    [ObservableProperty] private string _rpmText = "--";

    /// <summary>
    /// What the motherboard chip reports it is applying right now. In manual mode this is the way to
    /// tell whether the hardware accepted the slider value (the slider shows what was requested).
    /// </summary>
    [ObservableProperty] private string _reportedPercentText = "--";

    /// <summary>
    /// False only for channels that never had a fan: nothing is spinning now, it is not under manual
    /// control and it is not in the history of fans seen spinning (<see cref="AppSettings.FansSeenSpinning"/>).
    /// The page hides them. A fan that spun before and stopped stays visible so the problem is noticed.
    /// </summary>
    [ObservableProperty] private bool _isConnected = true;

    /// <summary>A fan seen spinning before now reads 0 RPM (a few readings in a row): it may have failed.</summary>
    [ObservableProperty] private bool _isStopped;

    [ObservableProperty] private string _stoppedText = string.Empty;

    public FanChannelViewModel(FanChannel channel, IFanControlService fans, ISettingsService settings)
    {
        _fans = fans;
        _settings = settings;
        Id = channel.Id;
        Name = channel.Name;
        HardwareName = channel.HardwareName;

        _displayName = settings.Current.FanNames.TryGetValue(Id, out var saved) && !string.IsNullOrWhiteSpace(saved)
            ? saved
            : Name;

        Update(channel);

        // Initial position of the slider only; afterwards it never follows the RPM.
        if (channel.CurrentPercent is float initial)
        {
            _updating = true;
            try { Percent = initial; }
            finally { _updating = false; }
        }
    }

    partial void OnDisplayNameChanged(string value)
    {
        var name = (value ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            DisplayName = Name; // cleared: back to the generic name (re-enters this method once)
            return;
        }

        if (name == Name) _settings.Current.FanNames.Remove(Id);
        else _settings.Current.FanNames[Id] = name;

        _settings.Save();
    }

    partial void OnIsManualChanged(bool value)
    {
        if (_updating) return;

        if (!value)
        {
            _fans.SetAutomatic(Id);
            return;
        }

        // Starting manual mode keeps the speed the fan is running at right now: the slider jumps there once.
        if (_reportedPercent is double running)
        {
            _updating = true;
            try { Percent = running; }
            finally { _updating = false; }
        }

        _fans.SetManual(Id, (float)Percent);
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
            ReportedPercentText = channel.CurrentPercent is float reported ? $"{reported:0} %" : "--";
            IsManual = channel.IsManual;
            _reportedPercent = channel.CurrentPercent;

            var spinning = (channel.Rpm ?? 0) > 0;

            // History: remember every channel that ever spun, so a later failure is not mistaken for an empty header.
            if (spinning && _settings.Current.FansSeenSpinning.Add(Id)) _settings.Save();
            var hasSpun = _settings.Current.FansSeenSpinning.Contains(Id);

            if (channel.IsManual || spinning) _idleReadings = 0;
            else if (_idleReadings < IdleReadingsToWarn) _idleReadings++;

            IsConnected = channel.IsManual || spinning || hasSpun;
            IsStopped = hasSpun && !channel.IsManual && !spinning && _idleReadings >= IdleReadingsToWarn;
            StoppedText = IsStopped ? LocalizationService.Instance.Format("Alert_Fan", DisplayName) : string.Empty;

            // The slider is the speed the user asked for: it does not move with the RPM.
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
    /// <summary>Common motherboard header labels offered as suggestions when naming a fan.</summary>
    public static IReadOnlyList<string> SuggestedNames { get; } = new[]
    {
        "CPU_FAN1", "CPU_FAN2", "CHA_FAN1", "CHA_FAN2", "CHA_FAN3", "CHA_FAN4", "PWR_FAN1", "AIO_PUMP"
    };

    private readonly ISensorService _sensors;
    private readonly IFanControlService _fans;
    private readonly ISettingsService _settings;

    public ObservableCollection<FanChannelViewModel> Channels { get; } = new();

    [ObservableProperty] private bool _isEmpty = true;

    public FansViewModel(ISensorService sensors, IFanControlService fans, ISettingsService settings)
    {
        _sensors = sensors;
        _fans = fans;
        _settings = settings;
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
                Channels.Add(new FanChannelViewModel(channel, _fans, _settings));
        }
        else
        {
            for (var i = 0; i < channels.Count; i++)
                Channels[i].Update(channels[i]);
        }

        IsEmpty = !Channels.Any(c => c.IsConnected);
    }

    [RelayCommand]
    private void RestoreAll()
    {
        _fans.RestoreAll();
        Refresh();
    }

    public void Dispose() => _sensors.SnapshotUpdated -= OnSnapshot;
}
