using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RiseModeMonitor.Core.Models;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.Services;

/// <summary>Stores settings as JSON in %AppData%\RiseModeMonitor\settings.json.</summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RiseModeMonitor",
        "settings.json");

    public AppSettings Current { get; }

    public JsonSettingsService() => Current = Load();

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options));
        }
        catch (IOException) { /* TODO: logging */ }
        catch (UnauthorizedAccessException) { /* TODO: logging */ }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupted or unreadable file: fall back to defaults.
        }
        return new AppSettings();
    }
}
