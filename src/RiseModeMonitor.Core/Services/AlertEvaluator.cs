using RiseModeMonitor.Core.Models;

namespace RiseModeMonitor.Core.Services;

public enum AlertKind { CpuTemperature, GpuTemperature, FanStopped }

/// <param name="Kind">What triggered the alert.</param>
/// <param name="Value">Measured value (temperature in °C; 0 for a stopped fan).</param>
/// <param name="Threshold">Configured limit (0 for a stopped fan).</param>
/// <param name="Detail">Extra text, e.g. the name of the stopped fan.</param>
public sealed record AlertEvent(AlertKind Kind, double Value, double Threshold, string? Detail = null);

/// <summary>Result of one evaluation cycle.</summary>
/// <param name="Notifications">Alerts that must show a notification now (each fires once per occurrence).</param>
/// <param name="PlaySound">True when the alert sound must play now (repeats while a condition persists).</param>
public sealed record AlertEvaluation(IReadOnlyList<AlertEvent> Notifications, bool PlaySound);

/// <summary>
/// Decides when alerts must notify and make noise. Pure logic (no UI, no audio), so it is easy to test.
/// <list type="bullet">
/// <item>Notification: shown once when the limit is reached; it re-arms only after the temperature drops
/// <see cref="HysteresisC"/> below the limit, and never more than once per <see cref="MinNotificationInterval"/>.</item>
/// <item>Sound: plays immediately and then every <see cref="AppSettings.AlertSoundIntervalSeconds"/> for as long as a
/// condition holds (temperature at or above its limit, or a fan that stopped and has not recovered). It stops as soon
/// as every condition is back to normal, so a game or a song cannot hide the alert.</item>
/// <item>Fan: the caller passes only the channels whose alert is on (see <see cref="AppSettings.IsFanAlertEnabled"/>).
/// One counts as stopped after <see cref="StoppedReadingsRequired"/> consecutive 0 RPM readings if it was seen
/// spinning in this session, or after <see cref="StartupReadingsRequired"/> if it has not spun yet (a fan that is
/// dead at boot), to ignore sensor glitches while everything settles. A channel that is no longer passed is
/// forgotten, which stops its alert.</item>
/// </list>
/// </summary>
public sealed class AlertEvaluator
{
    public const double HysteresisC = 5;
    public const int StoppedReadingsRequired = 3;
    public const int StartupReadingsRequired = 10;
    public static readonly TimeSpan MinNotificationInterval = TimeSpan.FromMinutes(1);
    public const int MinSoundIntervalSeconds = 3;
    public const int MaxSoundIntervalSeconds = 120;

    private readonly HashSet<AlertKind> _disarmed = new();
    private readonly Dictionary<AlertKind, DateTimeOffset> _lastNotified = new();
    private readonly HashSet<string> _spinning = new();
    private readonly HashSet<string> _stopped = new();
    private readonly Dictionary<string, int> _zeroReadings = new();
    private DateTimeOffset _lastSound = DateTimeOffset.MinValue;

    public AlertEvaluation Evaluate(
        DisplayTelemetry telemetry,
        IReadOnlyList<FanChannel> fans,
        AppSettings settings,
        DateTimeOffset now)
    {
        var notifications = new List<AlertEvent>();
        var active = false;

        active |= CheckTemperature(AlertKind.CpuTemperature, settings.AlertCpuEnabled, telemetry.CpuTemperatureC,
            settings.AlertCpuThreshold, now, notifications);
        active |= CheckTemperature(AlertKind.GpuTemperature, settings.AlertGpuEnabled, telemetry.GpuTemperatureC,
            settings.AlertGpuThreshold, now, notifications);

        if (settings.AlertFanStoppedEnabled)
        {
            CheckFans(fans, notifications);
            active |= _stopped.Count > 0;
        }
        else
        {
            ForgetFans();
        }

        // While something is wrong the sound repeats at the interval chosen by the user.
        var interval = TimeSpan.FromSeconds(
            Math.Clamp(settings.AlertSoundIntervalSeconds, MinSoundIntervalSeconds, MaxSoundIntervalSeconds));
        var playSound = active && now - _lastSound >= interval;
        if (playSound) _lastSound = now;

        return new AlertEvaluation(notifications, playSound);
    }

    /// <summary>Returns true while the temperature is at or above its limit.</summary>
    private bool CheckTemperature(
        AlertKind kind, bool enabled, double? value, double threshold, DateTimeOffset now, List<AlertEvent> notifications)
    {
        if (!enabled)
        {
            _disarmed.Remove(kind);
            return false;
        }

        if (value is not double temperature || !double.IsFinite(temperature)) return false;

        // The sound follows the limit itself; only the notification uses the re-arm margin.
        var overLimit = temperature >= threshold;

        if (temperature <= threshold - HysteresisC)
        {
            _disarmed.Remove(kind); // cooled down enough: the next crossing notifies again
        }
        else if (overLimit && !_disarmed.Contains(kind) &&
                 (!_lastNotified.TryGetValue(kind, out var last) || now - last >= MinNotificationInterval))
        {
            _disarmed.Add(kind);
            _lastNotified[kind] = now;
            notifications.Add(new AlertEvent(kind, temperature, threshold));
        }

        return overLimit;
    }

    private void CheckFans(IReadOnlyList<FanChannel> fans, List<AlertEvent> notifications)
    {
        ForgetUnlisted(fans);

        foreach (var fan in fans)
        {
            var rpm = fan.Rpm ?? 0;

            if (rpm > 0)
            {
                _spinning.Add(fan.Id);
                _zeroReadings.Remove(fan.Id);
                _stopped.Remove(fan.Id); // recovered: the sound stops
                continue;
            }

            if (_stopped.Contains(fan.Id)) continue;

            // A fan seen spinning needs few readings to count as stopped; one that has not spun yet gets a
            // longer grace period while the sensors and the fan settle after boot.
            var required = _spinning.Contains(fan.Id) ? StoppedReadingsRequired : StartupReadingsRequired;
            var zeros = _zeroReadings.GetValueOrDefault(fan.Id) + 1;
            _zeroReadings[fan.Id] = zeros;

            if (zeros >= required)
            {
                _stopped.Add(fan.Id);
                notifications.Add(new AlertEvent(AlertKind.FanStopped, 0, 0, fan.Name));
            }
        }
    }

    /// <summary>Forgets the channels that are no longer passed (alert turned off): their alert stops.</summary>
    private void ForgetUnlisted(IReadOnlyList<FanChannel> fans)
    {
        var listed = new HashSet<string>(fans.Select(f => f.Id));
        _spinning.RemoveWhere(id => !listed.Contains(id));
        _stopped.RemoveWhere(id => !listed.Contains(id));

        foreach (var id in _zeroReadings.Keys.Where(id => !listed.Contains(id)).ToList())
            _zeroReadings.Remove(id);
    }

    private void ForgetFans()
    {
        _spinning.Clear();
        _stopped.Clear();
        _zeroReadings.Clear();
    }
}
