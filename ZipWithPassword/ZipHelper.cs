using System.IO;
using Ionic.Zip;

namespace ZipWithPassword;

public static class ZipHelper
{
    // ── Create encrypted ZIP ────────────────────────────────────────────────
    public static void CreateEncryptedZip(
        string sourcePath,
        string outputPath,
        string password,
        IProgress<(int percent, string status)>? progress = null)
    {
        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        bool isDirectory = Directory.Exists(sourcePath);

        using var zip = new ZipFile();
        zip.Password         = password;
        zip.Encryption       = EncryptionAlgorithm.WinZipAes256;
        zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression;

        zip.SaveProgress += (_, args) =>
        {
            if (args.EventType == ZipProgressEventType.Saving_BeforeWriteEntry && args.EntriesTotal > 0)
            {
                int pct = (int)((double)args.EntriesSaved / args.EntriesTotal * 100);
                progress?.Report((pct, $"Compressing {Path.GetFileName(args.CurrentEntry.FileName)}…"));
            }
            else if (args.EventType == ZipProgressEventType.Saving_Completed)
            {
                progress?.Report((100, "Done"));
            }
        };

        if (isDirectory)
            zip.AddDirectory(sourcePath, new DirectoryInfo(sourcePath).Name);
        else
            zip.AddFile(sourcePath, string.Empty);

        zip.Save(outputPath);
    }

    // ── Extract encrypted ZIP ───────────────────────────────────────────────
    public static void ExtractEncryptedZip(
        string zipPath,
        string outputFolder,
        string password,
        IProgress<(int percent, string status)>? progress = null)
    {
        Directory.CreateDirectory(outputFolder);

        using var zip = ZipFile.Read(zipPath);
        zip.Password = password;

        int total  = zip.Count;
        int done   = 0;

        zip.ExtractProgress += (_, args) =>
        {
            if (args.EventType == ZipProgressEventType.Extracting_AfterExtractEntry)
            {
                done++;
                int pct = total > 0 ? (int)((double)done / total * 100) : 0;
                progress?.Report((pct, $"Extracting {Path.GetFileName(args.CurrentEntry.FileName)}…"));
            }
            else if (args.EventType == ZipProgressEventType.Extracting_AfterExtractAll)
            {
                progress?.Report((100, "Done"));
            }
        };

        foreach (var entry in zip)
            entry.Extract(outputFolder, ExtractExistingFileAction.OverwriteSilently);
    }
}
