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
/// <item>Sound: plays immediately and then every <see cref="SoundInterval"/> for as long as a condition holds
/// (temperature at or above its limit, or a fan that stopped and has not recovered). It stops as soon as every
/// condition is back to normal, so a game or a song cannot hide the alert.</item>
/// <item>Fan: a fan counts as stopped after <see cref="StoppedReadingsRequired"/> consecutive 0 RPM readings of a
/// channel that was previously spinning, to ignore sensor glitches and empty headers.</item>
/// </list>
/// </summary>
public sealed class AlertEvaluator
{
    public const double HysteresisC = 5;
    public const int StoppedReadingsRequired = 3;
    public static readonly TimeSpan MinNotificationInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan SoundInterval = TimeSpan.FromSeconds(10);

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

        var playSound = active && now - _lastSound >= SoundInterval;
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

            // Only fans that were seen spinning count; empty headers always read 0 RPM.
            if (!_spinning.Contains(fan.Id) || _stopped.Contains(fan.Id)) continue;

            var zeros = _zeroReadings.GetValueOrDefault(fan.Id) + 1;
            _zeroReadings[fan.Id] = zeros;

            if (zeros >= StoppedReadingsRequired)
            {
                _stopped.Add(fan.Id);
                notifications.Add(new AlertEvent(AlertKind.FanStopped, 0, 0, fan.Name));
            }
        }
    }

    private void ForgetFans()
    {
        _spinning.Clear();
        _stopped.Clear();
        _zeroReadings.Clear();
    }
}
