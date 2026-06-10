using System.IO;
using System.Text.Json;

namespace Starkive;

public class AppSettings
{
    public string DefaultOutputFolder  { get; set; } = string.Empty;
    public bool   ContextMenuInstalled { get; set; } = false;
    public int    MaxHistoryEntries    { get; set; } = 100;
    public bool   SidebarCollapsed     { get; set; } = false;
    public bool   AuditTrailEnabled    { get; set; } = false;
    public string Theme                { get; set; } = "Light";
    public string LastSeenVersion      { get; set; } = "";
    // Security / notification prefs
    public bool   ClearClipboardAfterCopy { get; set; } = true;   // auto-clear clipboard after 30s
    public int    ClipboardClearSeconds   { get; set; } = 30;
}

public static class SettingsManager
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Starkive", "settings.json");

    private static AppSettings? _cached;

    public static AppSettings Load()
    {
        if (_cached != null) return _cached;
        try
        {
            if (File.Exists(_filePath))
            {
                _cached = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath))
                       ?? new AppSettings();
                return _cached;
            }
        }
        catch { }
        _cached = new AppSettings();
        return _cached;
    }

    public static void Save(AppSettings settings)
    {
        _cached = settings;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
