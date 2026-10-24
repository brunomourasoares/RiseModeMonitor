using RiseModeMonitor.Core.Models;

namespace RiseModeMonitor.Core.Services;

public enum AlertKind { CpuTemperature, GpuTemperature, FanStopped }

/// <param name="Kind">What triggered the alert.</param>
/// <param name="Value">Measured value (temperature in °C; 0 for a stopped fan).</param>
/// <param name="Threshold">Configured limit (0 for a stopped fan).</param>
/// <param name="Detail">Extra text, e.g. the name of the stopped fan.</param>
public sealed record AlertEvent(AlertKind Kind, double Value, double Threshold, string? Detail = null);

/// <summary>
/// Decides when an alert should fire. Pure logic (no UI, no audio), so it is easy to test.
/// Temperature alerts fire once when the limit is reached and re-arm only after the temperature
/// drops <see cref="HysteresisC"/> below the limit; the same alert never repeats within
/// <see cref="MinInterval"/>. A fan alert needs <see cref="StoppedReadingsRequired"/> consecutive
/// 0 RPM readings of a fan that was previously spinning, to ignore sensor glitches.
/// </summary>
public sealed class AlertEvaluator
{
    public const double HysteresisC = 5;
    public const int StoppedReadingsRequired = 3;
    public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(1);

    private readonly HashSet<AlertKind> _disarmed = new();
    private readonly Dictionary<AlertKind, DateTimeOffset> _lastFired = new();
    private readonly HashSet<string> _spinning = new();
    private readonly HashSet<string> _stoppedReported = new();
    private readonly Dictionary<string, int> _zeroReadings = new();

    public IReadOnlyList<AlertEvent> Evaluate(
        DisplayTelemetry telemetry,
        IReadOnlyList<FanChannel> fans,
        AppSettings settings,
        DateTimeOffset now)
    {
        var events = new List<AlertEvent>();

        CheckTemperature(AlertKind.CpuTemperature, settings.AlertCpuEnabled, telemetry.CpuTemperatureC,
            settings.AlertCpuThreshold, now, events);
        CheckTemperature(AlertKind.GpuTemperature, settings.AlertGpuEnabled, telemetry.GpuTemperatureC,
            settings.AlertGpuThreshold, now, events);

        if (settings.AlertFanStoppedEnabled) CheckFans(fans, events);
        else ForgetFans();

        return events;
    }

    private void CheckTemperature(
        AlertKind kind, bool enabled, double? value, double threshold, DateTimeOffset now, List<AlertEvent> events)
    {
        if (!enabled)
        {
            _disarmed.Remove(kind);
            return;
        }

        if (value is not double temperature || !double.IsFinite(temperature)) return;

        if (temperature <= threshold - HysteresisC)
        {
            _disarmed.Remove(kind); // cooled down: ready for the next alert
            return;
        }

        if (temperature < threshold || _disarmed.Contains(kind)) return;

        if (_lastFired.TryGetValue(kind, out var last) && now - last < MinInterval) return;

        _disarmed.Add(kind);
        _lastFired[kind] = now;
        events.Add(new AlertEvent(kind, temperature, threshold));
    }

    private void CheckFans(IReadOnlyList<FanChannel> fans, List<AlertEvent> events)
    {
        foreach (var fan in fans)
        {
            var rpm = fan.Rpm ?? 0;

            if (rpm > 0)
            {
                _spinning.Add(fan.Id);
                _zeroReadings.Remove(fan.Id);
                _stoppedReported.Remove(fan.Id);
                continue;
            }

            // Only fans that were seen spinning count; empty headers always read 0 RPM.
            if (!_spinning.Contains(fan.Id) || _stoppedReported.Contains(fan.Id)) continue;

            var zeros = _zeroReadings.GetValueOrDefault(fan.Id) + 1;
            _zeroReadings[fan.Id] = zeros;

            if (zeros >= StoppedReadingsRequired)
            {
                _stoppedReported.Add(fan.Id);
                events.Add(new AlertEvent(AlertKind.FanStopped, 0, 0, fan.Name));
            }
        }
    }

    private void ForgetFans()
    {
        _spinning.Clear();
        _stoppedReported.Clear();
        _zeroReadings.Clear();
    }
}
