using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starkive;

/// <summary>
/// DPAPI-encrypted store of per-file saved passwords.
/// Lookup key is a SHA-256 of the output path (normalized) — never the path itself.
/// The entire file is encrypted with CurrentUser scope, same as auth.json.
/// </summary>
internal static class SavedPasswordStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Starkive", "saved_passwords.json");

    private static List<SavedEntry> _entries = [];

    internal static void Initialize()
    {
        try
        {
            if (!File.Exists(StorePath)) return;
            var encrypted = File.ReadAllBytes(StorePath);
            var plaintext = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            _entries = JsonSerializer.Deserialize<List<SavedEntry>>(
                Encoding.UTF8.GetString(plaintext)) ?? [];
        }
        catch { _entries = []; }
    }

    /// <summary>
    /// Save a password for a given output file path.
    /// </summary>
    internal static void Save(string outputPath, string password)
    {
        string key = MakeKey(outputPath);
        _entries.RemoveAll(e => e.Key == key);
        _entries.Add(new SavedEntry(
            Key:       key,
            Hint:      Path.GetFileName(outputPath),
            Password:  password,
            SavedAt:   DateTime.UtcNow));
        Persist();
    }

    /// <summary>
    /// Look up a saved password by output file path. Returns null if not found.
    /// </summary>
    internal static SavedEntry? Find(string filePath)
    {
        string key = MakeKey(filePath);
        return _entries.FirstOrDefault(e => e.Key == key);
    }

    internal static void Delete(string filePath)
    {
        string key = MakeKey(filePath);
        if (_entries.RemoveAll(e => e.Key == key) > 0) Persist();
    }

    // ── Cloud sync support ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current entries as unencrypted UTF-8 JSON bytes.
    /// Used by VaultSyncManager before encrypting for cloud upload.
    /// </summary>
    internal static byte[] ExportPlaintextJson()
        => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_entries));

    /// <summary>
    /// Merges entries from cloud plaintext JSON into local store.
    /// Cloud wins on conflict (newer SavedAt wins per key).
    /// </summary>
    internal static void MergeFromPlaintextJson(byte[] utf8Json)
    {
        try
        {
            var remote = JsonSerializer.Deserialize<List<SavedEntry>>(
                Encoding.UTF8.GetString(utf8Json)) ?? [];

            bool changed = false;
            foreach (var remoteEntry in remote)
            {
                var local = _entries.FirstOrDefault(e => e.Key == remoteEntry.Key);
                if (local == null)
                {
                    _entries.Add(remoteEntry);
                    changed = true;
                }
                else if (remoteEntry.SavedAt > local.SavedAt)
                {
                    _entries.Remove(local);
                    _entries.Add(remoteEntry);
                    changed = true;
                }
            }
            if (changed) Persist();
        }
        catch (Exception ex)
        {
            AppLog.Write($"VaultMerge error: {ex.Message}");
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static string MakeKey(string path)
    {
        // Normalize: lowercase, forward slashes, trim trailing separator
        string norm = path.Trim().ToLowerInvariant()
                          .Replace('\\', '/')
                          .TrimEnd('/');
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(norm));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var json      = JsonSerializer.Serialize(_entries);
            var plaintext = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(StorePath, encrypted);
        }
        catch { }

        // Fire-and-forget cloud push
        _ = CloudBackup.VaultSyncManager.PushAsync();
    }
}

internal sealed record SavedEntry(
    [property: JsonPropertyName("key")]      string   Key,
    [property: JsonPropertyName("hint")]     string   Hint,
    [property: JsonPropertyName("password")] string   Password,
    [property: JsonPropertyName("saved_at")] DateTime SavedAt
);
