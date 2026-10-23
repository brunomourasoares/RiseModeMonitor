using RiseModeMonitor.Core.Models;

namespace RiseModeMonitor.Hid;

/// <summary>
/// Encodes the 11-byte telemetry payload observed in the original vendor software
/// (VID 0xAA88, PID 0x8666). See docs/display-protocol.md.
/// Do not change the framing without testing against the physical cooler.
/// </summary>
public static class DisplayReport
{
    public const int PayloadLength = 11;
    public const int CpuTemperatureIndex = 1;
    public const int GpuTemperatureIndex = 2;
    public const int CpuLoadIndex = 3;
    public const int MemoryLoadIndex = 4;
    public const int MotherboardTemperatureIndex = 5;
    public const int GpuLoadIndex = 6;

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> when the CPU temperature is missing or
    /// outside 0-100 °C (the display needs a valid CPU temperature). Other fields fall back to 0.
    /// </summary>
    public static byte[] Create(DisplayTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (!IsInRange(telemetry.CpuTemperatureC, 0, 100))
            throw new ArgumentOutOfRangeException(nameof(telemetry), "CPU temperature must be valid and between 0 and 100 °C.");

        var payload = new byte[PayloadLength];
        payload[0] = 0; // header / reserved
        payload[CpuTemperatureIndex] = ToByte(telemetry.CpuTemperatureC, 0, 100);
        payload[GpuTemperatureIndex] = ToByte(telemetry.GpuTemperatureC, 0, 150);
        payload[CpuLoadIndex] = ToByte(telemetry.CpuLoadPercent, 0, 100);
        payload[MemoryLoadIndex] = ToByte(telemetry.MemoryLoadPercent, 0, 100);
        payload[MotherboardTemperatureIndex] = ToByte(telemetry.MotherboardTemperatureC, 0, 150);
        payload[GpuLoadIndex] = ToByte(telemetry.GpuLoadPercent, 0, 100);
        // Bytes 7-10 are reserved in the original protocol and stay zero.
        return payload;
    }

    private static bool IsInRange(double? value, double min, double max) =>
        value is double number && double.IsFinite(number) && number >= min && number <= max;

    private static byte ToByte(double? value, double min, double max) =>
        IsInRange(value, min, max) ? checked((byte)value!.Value) : (byte)0;
}
