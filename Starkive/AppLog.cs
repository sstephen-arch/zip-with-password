using System.IO;

namespace Starkive;

/// <summary>
/// Lightweight rolling log file — written to %AppData%\Starkive\starkive.log.
/// Kept to ~200 KB max; older lines are trimmed automatically.
/// </summary>
internal static class AppLog
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Starkive", "starkive.log");

    private static readonly object _lock = new();

    internal static void Write(string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z] {message}{Environment.NewLine}";
                File.AppendAllText(_path, line);

                // Trim file if > 200 KB
                var info = new FileInfo(_path);
                if (info.Length > 200 * 1024)
                {
                    var lines = File.ReadAllLines(_path);
                    File.WriteAllLines(_path, lines.Skip(lines.Length / 2));
                }
            }
        }
        catch { /* never crash the app over logging */ }
    }
}
