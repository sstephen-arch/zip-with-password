using System.Diagnostics;
using Microsoft.Win32;

namespace ZipWithPassword;

public static class ContextMenuInstaller
{
    private const string MenuLabel  = "Secure Starzip — Zip with password…";
    private const string SubKeyName = "SecureStarzip";

    private static readonly string[] TargetPaths =
    [
        @"SOFTWARE\Classes\*\shell",
        @"SOFTWARE\Classes\Directory\shell",
        @"SOFTWARE\Classes\Directory\Background\shell",
    ];

    public static void Install()
    {
        string exePath = GetExePath();
        RegistryKey root = TryOpenHklmWritable() ?? Registry.CurrentUser;

        foreach (string basePath in TargetPaths)
        {
            using var shellKey = root.OpenSubKey(basePath, writable: true)
                              ?? root.CreateSubKey(basePath);
            if (shellKey is null) continue;

            using var menuKey = shellKey.CreateSubKey(SubKeyName);
            menuKey.SetValue("", MenuLabel);
            menuKey.SetValue("Icon", $"\"{exePath}\"");

            using var cmdKey = menuKey.CreateSubKey("command");
            // Directory\Background passes folder path via %V; others via %1
            bool isBackground = basePath.Contains("Background");
            cmdKey.SetValue("", isBackground
                ? $"\"{exePath}\" \"%V\""
                : $"\"{exePath}\" \"%1\"");
        }

        SettingsManager.Load().ContextMenuInstalled = true;
        SettingsManager.Save(SettingsManager.Load());
    }

    public static void Uninstall()
    {
        RegistryKey root = TryOpenHklmWritable() ?? Registry.CurrentUser;
        foreach (string basePath in TargetPaths)
        {
            using var shellKey = root.OpenSubKey(basePath, writable: true);
            shellKey?.DeleteSubKeyTree(SubKeyName, throwOnMissingSubKey: false);
        }

        var s = SettingsManager.Load();
        s.ContextMenuInstalled = false;
        SettingsManager.Save(s);
    }

    public static bool IsInstalled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Classes\*\shell\{SubKeyName}");
            return key != null;
        }
        catch
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    $@"SOFTWARE\Classes\*\shell\{SubKeyName}");
                return key != null;
            }
            catch { return false; }
        }
    }

    private static string GetExePath()
        => Process.GetCurrentProcess().MainModule?.FileName
           ?? throw new InvalidOperationException("Cannot determine executable path.");

    private static RegistryKey? TryOpenHklmWritable()
    {
        try { return Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes", writable: true); }
        catch { return null; }
    }
}
