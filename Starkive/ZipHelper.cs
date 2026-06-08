using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace Starkive;

public static class ZipHelper
{
    // ── Public API ────────────────────────────────────────────────────────────

    public static void CreateEncryptedZip(string sourcePath, string outputPath, string password,
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);

        using var outStream = File.Create(outputPath);
        WriteZipToStream(sourcePath, outStream, password, progress, ct);
    }

    public static void ExtractEncryptedZip(string zipPath, string outputFolder, string password,
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);
        ExtractZipFromStream(File.OpenRead(zipPath), outputFolder, password, progress, ct);
    }

    // ── Internal — used by SszHelper to embed the ZIP in an SSZ container ────

    internal static byte[] CreateEncryptedZipBytes(string sourcePath, string password,
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        WriteZipToStream(sourcePath, ms, password, progress, ct);
        return ms.ToArray();
    }

    internal static void ExtractEncryptedZipFromBytes(byte[] zipBytes, string outputFolder,
        string password,
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);
        using var ms = new MemoryStream(zipBytes);
        ExtractZipFromStream(ms, outputFolder, password, progress, ct);
    }

    // ── Core implementation ───────────────────────────────────────────────────

    private static void WriteZipToStream(string sourcePath, Stream output, string password,
        IProgress<(int percent, string status)>? progress, CancellationToken ct)
    {
        string[] files = Directory.Exists(sourcePath)
            ? Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories)
            : [sourcePath];

        int total = Math.Max(files.Length, 1), done = 0;

        string baseName = Directory.Exists(sourcePath)
            ? new DirectoryInfo(sourcePath).Name
            : string.Empty;

        using var zip = new ZipOutputStream(output) { IsStreamOwner = false };
        zip.SetLevel(9);
        zip.Password = password;

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            string entryName = string.IsNullOrEmpty(baseName)
                ? Path.GetFileName(filePath)
                : Path.Combine(baseName, Path.GetRelativePath(sourcePath, filePath));

            entryName = ZipEntry.CleanName(entryName);

            var entry = new ZipEntry(entryName)
            {
                DateTime   = File.GetLastWriteTime(filePath),
                AESKeySize = 256,
            };

            progress?.Report((done * 100 / total, $"Compressing {Path.GetFileName(filePath)}..."));

            zip.PutNextEntry(entry);
            using (var fs = File.OpenRead(filePath))
                fs.CopyTo(zip);
            zip.CloseEntry();

            done++;
        }

        zip.Finish();
        progress?.Report((100, "Done"));
    }

    private static void ExtractZipFromStream(Stream source, string outputFolder, string password,
        IProgress<(int percent, string status)>? progress, CancellationToken ct)
    {
        string safeRoot = Path.GetFullPath(outputFolder).TrimEnd(Path.DirectorySeparatorChar)
                          + Path.DirectorySeparatorChar;

        using var zipFile = new ZipFile(source) { IsStreamOwner = false };
        zipFile.Password = password;

        int total = 0;
        foreach (ZipEntry e in zipFile) if (e.IsFile) total++;
        total = Math.Max(total, 1);

        int done = 0;
        foreach (ZipEntry entry in zipFile)
        {
            if (!entry.IsFile) continue;
            ct.ThrowIfCancellationRequested();

            string destPath = Path.GetFullPath(Path.Combine(outputFolder, entry.Name));
            if (!destPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Blocked unsafe ZIP entry: \"{entry.Name}\" resolves outside the output folder.");

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            progress?.Report((done * 100 / total, $"Extracting {Path.GetFileName(entry.Name)}..."));

            using var input  = zipFile.GetInputStream(entry);
            using var output = File.Create(destPath);
            input.CopyTo(output);

            done++;
        }

        progress?.Report((100, "Done"));
    }
}
