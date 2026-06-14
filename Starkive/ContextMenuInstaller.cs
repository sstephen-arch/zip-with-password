using System.Diagnostics;
using Microsoft.Win32;

namespace Starkive;

public static class ContextMenuInstaller
{
    private const string MenuLabel       = "Starkive — Zip with password...";
    private const string DecryptLabel    = "Decrypt with Starkive";
    private const string SubKeyName      = "Starkive";
    private const string DecryptKeyName  = "StarkiveDecrypt";

    private static readonly string[] TargetPaths =
    [
        @"SOFTWARE\Classes\*\shell",
        @"SOFTWARE\Classes\Directory\shell",
        @"SOFTWARE\Classes\Directory\Background\shell",
    ];

    // Paths where "Decrypt with Starkive" appears (zip + ssz files only)
    private static readonly string[] DecryptTargetPaths =
    [
        @"SOFTWARE\Classes\SystemFileAssociations\.zip\shell",
        @"SOFTWARE\Classes\SystemFileAssociations\.ssz\shell",
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
            if (menuKey is null) continue;
            menuKey.SetValue("", MenuLabel);
            menuKey.SetValue("Icon", $"\"{exePath}\"");
            // NeverDefault prevents this from becoming the default verb accidentally
            menuKey.SetValue("NeverDefault", "");

            using var cmdKey = menuKey.CreateSubKey("command");
            if (cmdKey is null) continue;
            bool isBackground = basePath.Contains("Background");
            cmdKey.SetValue("", isBackground
                ? $"\"{exePath}\" \"%V\""
                : $"\"{exePath}\" \"%1\"");
        }

        // "Decrypt with Starkive" on .zip and .ssz files
        foreach (string basePath in DecryptTargetPaths)
        {
            using var shellKey = root.OpenSubKey(basePath, writable: true)
                              ?? root.CreateSubKey(basePath);
            if (shellKey is null) continue;

            using var menuKey = shellKey.CreateSubKey(DecryptKeyName);
            if (menuKey is null) continue;
            menuKey.SetValue("", DecryptLabel);
            menuKey.SetValue("Icon", $"\"{exePath}\"");

            using var cmdKey = menuKey.CreateSubKey("command");
            cmdKey?.SetValue("", $"\"{exePath}\" --unzip \"%1\"");
        }

        RegisterFileAssociation();

        var s = SettingsManager.Load();
        s.ContextMenuInstalled = true;
        SettingsManager.Save(s);
    }

    public static void Uninstall()
    {
        RegistryKey root = TryOpenHklmWritable() ?? Registry.CurrentUser;

        foreach (string basePath in TargetPaths)
        {
            using var shellKey = root.OpenSubKey(basePath, writable: true);
            shellKey?.DeleteSubKeyTree(SubKeyName, throwOnMissingSubKey: false);
        }

        foreach (string basePath in DecryptTargetPaths)
        {
            using var shellKey = root.OpenSubKey(basePath, writable: true);
            shellKey?.DeleteSubKeyTree(DecryptKeyName, throwOnMissingSubKey: false);
        }

        UnregisterFileAssociation();

        var s = SettingsManager.Load();
        s.ContextMenuInstalled = false;
        SettingsManager.Save(s);
    }

    public static bool IsInstalled()
    {
        try
        {
            using var key = Registry.LocalMachine
                .OpenSubKey($@"SOFTWARE\Classes\*\shell\{SubKeyName}");
            return key != null;
        }
        catch
        {
            try
            {
                using var key = Registry.CurrentUser
                    .OpenSubKey($@"SOFTWARE\Classes\*\shell\{SubKeyName}");
                return key != null;
            }
            catch { return false; }
        }
    }

    // ─── .ssz file association ───────────────────────────────────────────────

    public static void RegisterFileAssociation()
    {
        string exePath = GetExePath();

        // Try HKLM first; fall back to HKCU silently
        RegistryKey root;
        try
        {
            var hklm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes", writable: true);
            root = hklm ?? Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(@"Software\Classes");
        }
        catch
        {
            root = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
        }

        using (root)
        {
            // .ssz extension → ProgID
            using var extKey = root.CreateSubKey(@".ssz");
            extKey?.SetValue("", "StarkiveSecureContainer");

            // ProgID definition
            using var progKey = root.CreateSubKey("StarkiveSecureContainer");
            if (progKey is null) return;
            progKey.SetValue("", "Starkive Secure Container");

            using var iconKey = progKey.CreateSubKey("DefaultIcon");
            string sszIcon = SszIconGenerator.EnsureIcon();
            iconKey?.SetValue("", $"\"{sszIcon}\",0");

            using var openKey = progKey.CreateSubKey(@"shell\open\command");
            openKey?.SetValue("", $"\"{exePath}\" \"%1\"");
        }
    }

    public static void UnregisterFileAssociation()
    {
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                string classesPath = root == Registry.LocalMachine
                    ? @"SOFTWARE\Classes"
                    : @"Software\Classes";

                using var classes = root.OpenSubKey(classesPath, writable: true);
                classes?.DeleteSubKeyTree(".ssz",                    throwOnMissingSubKey: false);
                classes?.DeleteSubKeyTree("StarkiveSecureContainer", throwOnMissingSubKey: false);
            }
            catch { }
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string GetExePath()
        => Process.GetCurrentProcess().MainModule?.FileName
           ?? throw new InvalidOperationException("Cannot determine executable path.");

    private static RegistryKey? TryOpenHklmWritable()
    {
        try { return Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes", writable: true); }
        catch { return null; }
    }
}
