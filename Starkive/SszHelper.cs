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
        // Build the encrypted ZIP payload in memory.
        progress?.Report((0, "Building encrypted payload..."));
        byte[] payload = ZipHelper.CreateEncryptedZipBytes(sourcePath, password, progress, ct);

        ct.ThrowIfCancellationRequested();

        // Compute SHA-256 of the payload for tamper detection.
        byte[] hash      = SHA256.HashData(payload);
        Guid   fileToken = Guid.NewGuid();
        string senderId  = AuthManager.UserId ?? new string('0', 36);

        // Pad/truncate sender ID to exactly 36 bytes (UUID string length).
        byte[] senderBytes = new byte[36];
        byte[] senderRaw   = Encoding.ASCII.GetBytes(senderId.PadRight(36)[..36]);
        Array.Copy(senderRaw, senderBytes, Math.Min(senderRaw.Length, 36));

        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);

        using var fs = File.Create(outputPath);
        using var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: false);

        // Header
        bw.Write(Magic);
        bw.Write(Version);
        bw.Write(fileToken.ToByteArray());  // 16 bytes
        bw.Write(senderBytes);              // 36 bytes
        bw.Write(hash);                     // 32 bytes
        bw.Write((long)payload.Length);     // 8 bytes
        // Total header: 4+4+16+36+32+8 = 100 bytes ✓

        // Payload
        bw.Write(payload);

        progress?.Report((100, "Done"));

        return new SszCreateResult(fileToken.ToString(), senderId, hash, payload.LongLength);
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

        // Validate magic
        byte[] magic = br.ReadBytes(4);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("Not a valid Starkive Secure Container (.ssz) file.");

        uint version = br.ReadUInt32();
        if (version != Version)
            throw new InvalidDataException($"Unsupported SSZ version: {version}. Update Starkive to open this file.");

        // Read header fields
        byte[] tokenBytes  = br.ReadBytes(16);
        byte[] senderBytes = br.ReadBytes(36);
        byte[] storedHash  = br.ReadBytes(32);
        long   payloadLen  = br.ReadInt64();

        string fileToken = new Guid(tokenBytes).ToString();

        // Read and verify payload
        progress?.Report((5, "Verifying file integrity..."));
        byte[] payload = br.ReadBytes((int)payloadLen);

        byte[] actualHash = SHA256.HashData(payload);
        if (!actualHash.SequenceEqual(storedHash))
            throw new InvalidDataException("File integrity check failed. The SSZ file may be corrupted or tampered with.");

        ct.ThrowIfCancellationRequested();

        // Phone-home: fire-and-forget, never blocks opening.
        _ = ApiService.ReportOpenAsync(fileToken);

        // Decrypt and extract payload
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

            return new SszHeader(
                Version:    version,
                FileToken:  new Guid(tokenBytes).ToString(),
                SenderId:   Encoding.ASCII.GetString(senderRaw).TrimEnd('\0', ' ')
            );
        }
        catch { return null; }
    }
}

// ── Result types ──────────────────────────────────────────────────────────────

internal sealed record SszCreateResult(
    string FileToken,
    string SenderId,
    byte[] PayloadHash,
    long   PayloadBytes
);

internal sealed record SszHeader(
    uint   Version,
    string FileToken,
    string SenderId
);
