using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Starkive;

/// <summary>
/// Creates and opens Starkive Secure Container (.ssz) files.
///
/// Binary layout (100-byte fixed header):
///   [0-3]   Magic:          53 53 5A 01  ("SSZ" + version byte)
///   [4-7]   Version:        uint32 LE    (currently 1)
///   [8-23]  File token:     16 bytes     (raw Guid — used for open-notification phone-home)
///   [24-59] Sender user ID: 36 bytes     (ASCII UUID string of the creating user)
///   [60-91] Payload SHA-256:32 bytes     (SHA-256 of the encrypted payload bytes)
///   [92-99] Payload length: int64 LE
///   [100..] Payload:        WinZip AES-256 encrypted ZIP (via SharpZipLib)
///
/// Star name: derived deterministically from the file token via StarNames.GetForToken().
/// It is NOT stored in the binary — it is always recomputed from the token.
/// </summary>
internal static class SszHelper
{
    private static readonly byte[] Magic   = [0x53, 0x53, 0x5A, 0x01];
    private const uint             Version = 1;
    private const int              HeaderSize = 100;

    // ── Create ────────────────────────────────────────────────────────────────

    internal static SszCreateResult Create(
        string sourcePath,
        string outputPath,
        string password,
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report((0, "Building encrypted payload..."));
        byte[] payload = ZipHelper.CreateEncryptedZipBytes(sourcePath, password, progress, ct);

        ct.ThrowIfCancellationRequested();

        byte[] hash      = SHA256.HashData(payload);
        Guid   fileToken = Guid.NewGuid();
        string starName  = StarNames.GetForToken(fileToken);
        string senderId  = AuthManager.UserId ?? new string('0', 36);

        byte[] senderBytes = new byte[36];
        byte[] senderRaw   = Encoding.ASCII.GetBytes(senderId.PadRight(36)[..36]);
        Array.Copy(senderRaw, senderBytes, Math.Min(senderRaw.Length, 36));

        // Rename the output path to include the star name before the extension
        // e.g. "MyFiles.ssz" → "MyFiles_Vega.ssz"
        string finalOutput = AppendStarToPath(outputPath, starName);

        string? outputDir = Path.GetDirectoryName(finalOutput);
        if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);

        using var fs = File.Create(finalOutput);
        using var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: false);

        bw.Write(Magic);
        bw.Write(Version);
        bw.Write(fileToken.ToByteArray());  // 16 bytes
        bw.Write(senderBytes);              // 36 bytes
        bw.Write(hash);                     // 32 bytes
        bw.Write((long)payload.Length);     // 8 bytes
        bw.Write(payload);

        progress?.Report((100, "Done"));

        return new SszCreateResult(fileToken.ToString(), senderId, hash, payload.LongLength, starName, finalOutput);
    }

    // ── Open ──────────────────────────────────────────────────────────────────

    internal static void Open(
        string sszPath,
        string outputFolder,
        string password,
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        using var fs = File.OpenRead(sszPath);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        byte[] magic = br.ReadBytes(4);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("Not a valid Starkive Secure Container (.ssz) file.");

        uint version = br.ReadUInt32();
        if (version != Version)
            throw new InvalidDataException($"Unsupported SSZ version: {version}. Update Starkive to open this file.");

        byte[] tokenBytes  = br.ReadBytes(16);
        byte[] senderBytes = br.ReadBytes(36);
        byte[] storedHash  = br.ReadBytes(32);
        long   payloadLen  = br.ReadInt64();

        Guid   tokenGuid = new Guid(tokenBytes);
        string fileToken = tokenGuid.ToString();
        string starName  = StarNames.GetForToken(tokenGuid);  // always derivable from token

        progress?.Report((5, "Verifying file integrity..."));
        byte[] payload = br.ReadBytes((int)payloadLen);

        byte[] actualHash = SHA256.HashData(payload);
        if (!actualHash.SequenceEqual(storedHash))
            throw new InvalidDataException("File integrity check failed. The SSZ file may be corrupted or tampered with.");

        ct.ThrowIfCancellationRequested();

        // NOTE: ReportOpenAsync is called by the UI layer after this returns,
        // so the caller can display the creator identity from the response.

        progress?.Report((10, "Decrypting..."));
        ZipHelper.ExtractEncryptedZipFromBytes(payload, outputFolder, password, progress, ct);
    }

    // ── Peek (read header only — no decryption) ───────────────────────────────

    internal static SszHeader? PeekHeader(string sszPath)
    {
        try
        {
            using var fs = File.OpenRead(sszPath);
            using var br = new BinaryReader(fs);

            byte[] magic = br.ReadBytes(4);
            if (!magic.SequenceEqual(Magic)) return null;

            uint   version    = br.ReadUInt32();
            byte[] tokenBytes = br.ReadBytes(16);
            byte[] senderRaw  = br.ReadBytes(36);

            var tokenGuid = new Guid(tokenBytes);
            return new SszHeader(
                Version:    version,
                FileToken:  tokenGuid.ToString(),
                SenderId:   Encoding.ASCII.GetString(senderRaw).TrimEnd('\0', ' '),
                StarName:   StarNames.GetForToken(tokenGuid)
            );
        }
        catch { return null; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts the star name before the file extension.
    /// "archive.ssz" → "archive_Vega.ssz"
    /// "archive"     → "archive_Vega.ssz"
    /// </summary>
    internal static string AppendStarToPath(string path, string starName)
    {
        string dir  = Path.GetDirectoryName(path) ?? "";
        string name = Path.GetFileNameWithoutExtension(path);
        string ext  = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext)) ext = ".ssz";

        // Don't double-append if the name already ends with the star
        if (name.EndsWith($"_{starName}", StringComparison.OrdinalIgnoreCase))
            return path;

        return Path.Combine(dir, $"{name}_{starName}{ext}");
    }
}

// ── Result types ──────────────────────────────────────────────────────────────

internal sealed record SszCreateResult(
    string FileToken,
    string SenderId,
    byte[] PayloadHash,
    long   PayloadBytes,
    string StarName,
    string FinalOutputPath   // may differ from requested path if star was appended
);

internal sealed record SszHeader(
    uint   Version,
    string FileToken,
    string SenderId,
    string StarName
);
