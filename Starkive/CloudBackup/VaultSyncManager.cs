using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Starkive.CloudBackup;

/// <summary>
/// Handles AES-256 encryption of the vault and sync with the connected cloud provider.
///
/// Security model:
///   • The vault (saved_passwords.json plaintext) is encrypted with AES-256-GCM
///     before leaving the device.
///   • The encryption key is derived from a 32-byte app-level secret that is itself
///     protected by DPAPI and stored in %AppData%\Starkive\cloud\vault_key.bin.
///   • The cloud provider never sees plaintext passwords.
/// </summary>
internal static class VaultSyncManager
{
    private static readonly string _keyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Starkive", "cloud", "vault_key.bin");

    private static readonly List<ICloudProvider> _providers =
    [
        new GoogleDriveProvider(),
        new OneDriveProvider(),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>All registered providers (connected or not).</summary>
    internal static IReadOnlyList<ICloudProvider> Providers => _providers;

    /// <summary>Any provider that is currently connected.</summary>
    internal static IEnumerable<ICloudProvider> ConnectedProviders
        => _providers.Where(p => p.IsConnected);

    /// <summary>
    /// Upload the current vault to all connected providers.
    /// Call this whenever a password is saved or deleted.
    /// </summary>
    internal static async Task PushAsync(CancellationToken ct = default)
    {
        if (!ConnectedProviders.Any()) return;

        byte[] plaintext = ReadLocalVaultPlaintext();
        byte[] encrypted = Encrypt(plaintext);

        foreach (var provider in ConnectedProviders)
        {
            try { await provider.UploadVaultAsync(encrypted, ct); }
            catch (Exception ex)
            {
                AppLog.Write($"VaultSync push error ({provider.ProviderName}): {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Pull the vault from the first connected provider that has a backup,
    /// merge with local, and save.  Call on startup.
    /// </summary>
    internal static async Task PullAndMergeAsync(CancellationToken ct = default)
    {
        foreach (var provider in ConnectedProviders)
        {
            try
            {
                byte[]? encrypted = await provider.DownloadVaultAsync(ct);
                if (encrypted == null) continue;

                byte[] plaintext = Decrypt(encrypted);
                MergeIntoLocalVault(plaintext);
                AppLog.Write($"VaultSync pulled & merged from {provider.ProviderName}");
                return; // one successful pull is enough
            }
            catch (Exception ex)
            {
                AppLog.Write($"VaultSync pull error ({provider.ProviderName}): {ex.Message}");
            }
        }
    }

    // ── Encryption (AES-256-GCM) ──────────────────────────────────────────────

    private static byte[] Encrypt(byte[] plaintext)
    {
        byte[] key   = GetOrCreateKey();
        byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag        = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Layout: [4 bytes magic][12 bytes nonce][16 bytes tag][ciphertext]
        byte[] result = new byte[4 + nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(new byte[] { 0x53, 0x56, 0x4C, 0x54 }, 0, result, 0, 4); // "SVLT"
        Buffer.BlockCopy(nonce,      0, result, 4,                    nonce.Length);
        Buffer.BlockCopy(tag,        0, result, 4 + nonce.Length,     tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, 4 + nonce.Length + tag.Length, ciphertext.Length);
        return result;
    }

    private static byte[] Decrypt(byte[] data)
    {
        if (data.Length < 4 + 12 + 16) throw new InvalidDataException("Invalid vault format.");
        // Verify magic
        if (data[0] != 0x53 || data[1] != 0x56 || data[2] != 0x4C || data[3] != 0x54)
            throw new InvalidDataException("Invalid vault magic.");

        byte[] key        = GetOrCreateKey();
        byte[] nonce      = data[4..16];
        byte[] tag        = data[16..32];
        byte[] ciphertext = data[32..];
        byte[] plaintext  = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    private static byte[] GetOrCreateKey()
    {
        try
        {
            if (File.Exists(_keyPath))
            {
                var enc = File.ReadAllBytes(_keyPath);
                return System.Security.Cryptography.ProtectedData.Unprotect(
                    enc, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            }
        }
        catch { /* generate a new key if DPAPI fails */ }

        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        Directory.CreateDirectory(Path.GetDirectoryName(_keyPath)!);
        var protected_ = System.Security.Cryptography.ProtectedData.Protect(
            key, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_keyPath, protected_);
        return key;
    }

    // ── Local vault helpers ───────────────────────────────────────────────────

    private static readonly string _vaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Starkive", "saved_passwords.json");

    private static byte[] ReadLocalVaultPlaintext()
    {
        // SavedPasswordStore stores DPAPI-encrypted bytes.
        // For cloud sync we need the JSON, so we ask SavedPasswordStore to export.
        return SavedPasswordStore.ExportPlaintextJson();
    }

    private static void MergeIntoLocalVault(byte[] cloudPlaintext)
    {
        SavedPasswordStore.MergeFromPlaintextJson(cloudPlaintext);
    }
}
