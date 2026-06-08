using System.IO;
using System.Text.Json;

namespace Starkive;

public enum OperationType { Zip, Unzip }

public class HistoryEntry
{
    public OperationType Type         { get; set; }
    public string        SourcePath   { get; set; } = string.Empty;
    public string        OutputPath   { get; set; } = string.Empty;
    public DateTime      Timestamp    { get; set; } = DateTime.Now;
    public bool          Success      { get; set; } = true;
    public string        ErrorMessage { get; set; } = string.Empty;
}

public static class HistoryManager
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Starkive", "history.json");

    public static List<HistoryEntry> Load()
    {
        try
        {
            if (File.Exists(_filePath))
                return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(_filePath)) ?? [];
        }
        catch { }
        return [];
    }

    public static void Add(HistoryEntry entry)
    {
        var list = Load();
        list.Insert(0, entry);
        var settings = SettingsManager.Load();
        if (list.Count > settings.MaxHistoryEntries)
            list = list.Take(settings.MaxHistoryEntries).ToList();
        Save(list);
    }

    public static void Clear() => Save([]);

    private static void Save(List<HistoryEntry> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath,
            JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }
}
