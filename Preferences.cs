using System.Text.Json;

namespace OPods;

/// <summary>
/// Simple JSON-backed preferences persisted to %APPDATA%/OPods/preferences.json.
/// Stores last device, connection method, and game-mode implementation.
/// </summary>
public static class Preferences
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OPods");
    private static readonly string FilePath = Path.Combine(Dir, "preferences.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public static string LastDeviceAddress { get; set; } = string.Empty;
    public static string LastDeviceName { get; set; } = string.Empty;
    public static string LastDeviceModel { get; set; } = string.Empty;
    public static string RfcommConnectionMethod { get; set; } = "uuid";
    public static string GameModeImplementation { get; set; } = "standard";

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            var data = JsonSerializer.Deserialize<PrefsData>(json, JsonOpts);
            if (data == null) return;
            LastDeviceAddress = data.LastDeviceAddress ?? string.Empty;
            LastDeviceName = data.LastDeviceName ?? string.Empty;
            LastDeviceModel = data.LastDeviceModel ?? string.Empty;
            RfcommConnectionMethod = data.RfcommConnectionMethod ?? "uuid";
            GameModeImplementation = data.GameModeImplementation ?? "standard";
        }
        catch
        {
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var data = new PrefsData
            {
                LastDeviceAddress = LastDeviceAddress,
                LastDeviceName = LastDeviceName,
                LastDeviceModel = LastDeviceModel,
                RfcommConnectionMethod = RfcommConnectionMethod,
                GameModeImplementation = GameModeImplementation
            };
            var json = JsonSerializer.Serialize(data, JsonOpts);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
        }
    }

    private sealed class PrefsData
    {
        public string LastDeviceAddress { get; set; } = string.Empty;
        public string LastDeviceName { get; set; } = string.Empty;
        public string LastDeviceModel { get; set; } = string.Empty;
        public string RfcommConnectionMethod { get; set; } = "uuid";
        public string GameModeImplementation { get; set; } = "standard";
    }
}
