# Display protocol notes

Reverse engineering of the original software indicates HID VID `0xAA88`, PID `0x8666`, and an 11-byte telemetry payload recovered from the original `tifPipe` path. The payload fields are:

| Payload byte | Meaning | Current app behavior |
|---:|---|---|
| 0 | Header/reserved | 0 |
| 1 | CPU temperature in °C | Required; values outside 0–100 °C are not sent |
| 2 | GPU temperature in °C | Valid 0–150 °C reading, otherwise 0 |
| 3 | CPU utilization % | Valid 0–100% reading, otherwise 0 |
| 4 | Memory utilization % | Valid 0–100% reading, otherwise 0 |
| 5 | Motherboard temperature in °C | 0; no validated sensor mapping yet |
| 6 | GPU utilization % | Valid 0–100% reading, otherwise 0 |
| 7–10 | Reserved | 0 |

The original software's legacy HID writer prepended Report ID 0 in its output path. However, the physical device interface discovered during validation advertises an 11-byte output report, and the app was verified against that interface by writing the 11-byte payload directly. The transport therefore requires an output report length of exactly 11 bytes and writes the 11-byte payload without adding a separate byte. Interfaces advertising a different length are rejected until they are validated against the physical cooler. **Do not change this framing without testing against the physical cooler.**

A successful Windows HID write only means the write completed without an exception; it does not prove that the physical display rendered the values. Validation on the actual cooler is still required.

## Fan and pump control status

No validated fan-speed or pump-speed command is established by the telemetry payload. The 11-byte report is treated as display telemetry only; its reserved bytes must not be repurposed as control fields based on guesswork. The application therefore does not expose or send fan/pump control commands for the cooler.

Before implementing control, capture traffic from the original vendor application while changing exactly one setting at a time. Record the HID interface, report ID/framing, direction, report length, command bytes, checksums, accepted ranges, and any response or acknowledgement. Compare repeated captures to isolate which bytes actually change, then validate the command and fail-safe behavior on the physical cooler with explicit user consent.

## Diagnostics

The HID service appends (rate-limited) diagnostic lines to `%LocalAppData%\RiseModeMonitor\hid-diagnostic.log`.

Source: protocol notes from the previous RMMonitor app.
