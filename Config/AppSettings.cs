using System.Text.Json;
using RyzoriaUI.Models;

namespace RyzoriaUI.Config;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string PerformanceMode { get; set; } = "Auto";
    public int ScrcpyMaxSize { get; set; } = 480;
    public int ScrcpyMaxFps { get; set; } = 20;
    public int ScrcpyBitrateMbps { get; set; } = 2;
    public bool Fullscreen { get; set; }
    public bool TurnScreenOff { get; set; }
    public bool NoAudio { get; set; } = true;
    public bool DeveloperMode { get; set; }
    public bool AutoReconnect { get; set; } = true;
    public bool ClipboardSync { get; set; }
    public bool Notifications { get; set; }
    public List<GameProfile> Games { get; set; } = new();
    public List<MapperProfile> MapperProfiles { get; set; } = new() { new MapperProfile() };
}

public static class SettingsStore
{
    public static string AppDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RyzoriaUI");
    public static string SettingsFile => Path.Combine(AppDirectory, "settings.json");
    public static string LogDirectory => Path.Combine(AppDirectory, "logs");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppDirectory);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        var temp = SettingsFile + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, SettingsFile, true);
    }

    public static void Reset() => Save(new AppSettings());
}
