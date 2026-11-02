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

    /// <summary>Consecutive 0 RPM readings (automatic mode, alert on) after which the card warns that the fan stopped.</summary>
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
    /// The stopped-fan alert is on for this channel (the switch on the card). The default is on for a fan that
    /// was seen spinning and off for an empty header; see <see cref="AppSettings.IsFanAlertEnabled"/>.
    /// </summary>
    [ObservableProperty] private bool _alertEnabled;

    /// <summary>
    /// Whether the card is shown: always when the alert is on or the channel is under manual control; otherwise
    /// only while the page is not hiding the fans with the alert off (<see cref="AppSettings.HideDisabledFans"/>).
    /// </summary>
    [ObservableProperty] private bool _isShown = true;

    /// <summary>The alert is on and the fan reads 0 RPM (a few readings in a row): it may have failed.</summary>
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

    partial void OnAlertEnabledChanged(bool value)
    {
        if (_updating) return;

        // The user flipped the switch: remember it, and react right away (the next reading would be a second late).
        _settings.Current.SetFanAlert(Id, value);
        _settings.Save();

        IsShown = IsManual || value || !_settings.Current.HideDisabledFans;
        if (!value)
        {
            IsStopped = false;
            StoppedText = string.Empty;
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

            // History: a channel that spun is expected to keep spinning, which makes its alert on by default.
            if (spinning && _settings.Current.MarkFanSeenSpinning(Id)) _settings.Save();
            AlertEnabled = _settings.Current.IsFanAlertEnabled(Id);

            if (channel.IsManual || spinning) _idleReadings = 0;
            else if (_idleReadings < IdleReadingsToWarn) _idleReadings++;

            IsShown = channel.IsManual || AlertEnabled || !_settings.Current.HideDisabledFans;
            IsStopped = AlertEnabled && !channel.IsManual && !spinning && _idleReadings >= IdleReadingsToWarn;
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

    /// <summary>Hide the fan cards whose alert is turned off.</summary>
    [ObservableProperty] private bool _hideDisabled;

    public FansViewModel(ISensorService sensors, IFanControlService fans, ISettingsService settings)
    {
        _sensors = sensors;
        _fans = fans;
        _settings = settings;
        _hideDisabled = settings.Current.HideDisabledFans;
        _sensors.SnapshotUpdated += OnSnapshot;
        Refresh();
    }

    partial void OnHideDisabledChanged(bool value)
    {
        _settings.Current.HideDisabledFans = value;
        _settings.Save();
        Refresh(); // re-evaluates which cards are shown
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

        IsEmpty = !Channels.Any(c => c.IsShown);
    }

    [RelayCommand]
    private void RestoreAll()
    {
        _fans.RestoreAll();
        Refresh();
    }

    public void Dispose() => _sensors.SnapshotUpdated -= OnSnapshot;
}
