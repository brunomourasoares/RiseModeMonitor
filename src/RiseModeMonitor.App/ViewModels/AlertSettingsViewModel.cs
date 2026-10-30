using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiseModeMonitor.Core.Services;
using RiseModeMonitor.Services;

namespace RiseModeMonitor.ViewModels;

/// <summary>Alert options shown in the Settings page (limits, sound, volume).</summary>
public sealed partial class AlertSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly AlertService _alerts;

    public IReadOnlyList<int> SoundOptions { get; } = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

    [ObservableProperty] private bool _cpuEnabled;
    [ObservableProperty] private double _cpuThreshold;
    [ObservableProperty] private bool _gpuEnabled;
    [ObservableProperty] private double _gpuThreshold;
    [ObservableProperty] private bool _fanStoppedEnabled;
    [ObservableProperty] private int _selectedSound;
    [ObservableProperty] private double _volume;
    [ObservableProperty] private double _soundInterval;

    public AlertSettingsViewModel(ISettingsService settings, AlertService alerts)
    {
        _settings = settings;
        _alerts = alerts;

        var current = settings.Current;
        _cpuEnabled = current.AlertCpuEnabled;
        _cpuThreshold = current.AlertCpuThreshold;
        _gpuEnabled = current.AlertGpuEnabled;
        _gpuThreshold = current.AlertGpuThreshold;
        _fanStoppedEnabled = current.AlertFanStoppedEnabled;
        _selectedSound = Math.Clamp(current.AlertSound, 1, 12);
        _volume = current.AlertVolume;
        _soundInterval = Math.Clamp(
            current.AlertSoundIntervalSeconds, AlertEvaluator.MinSoundIntervalSeconds, AlertEvaluator.MaxSoundIntervalSeconds);
    }

    partial void OnCpuEnabledChanged(bool value) => Save(s => s.AlertCpuEnabled = value);

    partial void OnCpuThresholdChanged(double value) => Save(s => s.AlertCpuThreshold = Math.Round(value));

    partial void OnGpuEnabledChanged(bool value) => Save(s => s.AlertGpuEnabled = value);

    partial void OnGpuThresholdChanged(double value) => Save(s => s.AlertGpuThreshold = Math.Round(value));

    partial void OnFanStoppedEnabledChanged(bool value) => Save(s => s.AlertFanStoppedEnabled = value);

    partial void OnSelectedSoundChanged(int value)
    {
        Save(s => s.AlertSound = value);
        _alerts.PlaySound(value, Volume); // preview when choosing
    }

    partial void OnVolumeChanged(double value) => Save(s => s.AlertVolume = Math.Round(value));

    partial void OnSoundIntervalChanged(double value) => Save(s => s.AlertSoundIntervalSeconds = (int)Math.Round(value));

    [RelayCommand]
    private void TestSound() => _alerts.PlaySound(SelectedSound, Volume);

    private void Save(Action<Core.Models.AppSettings> change)
    {
        change(_settings.Current);
        _settings.Save();
    }
}
