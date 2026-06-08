using System.Diagnostics;
using Microsoft.Win32;

namespace Starkive;

public static class ContextMenuInstaller
{
    private const string MenuLabel  = "Starkive — Zip with password...";
    private const string SubKeyName = "Starkive";

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
            bool isBackground = basePath.Contains("Background");
            cmdKey.SetValue("", isBackground
                ? $"\"{exePath}\" \"%V\""
                : $"\"{exePath}\" \"%1\"");
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
            using (var extKey = root.CreateSubKey(@".ssz"))
                extKey.SetValue("", "StarkiveSecureContainer");

            // ProgID definition
            using var progKey = root.CreateSubKey("StarkiveSecureContainer");
            progKey.SetValue("", "Starkive Secure Container");

            using (var iconKey = progKey.CreateSubKey("DefaultIcon"))
                iconKey.SetValue("", $"\"{exePath}\",0");

            using var openKey = progKey.CreateSubKey(@"shell\open\command");
            openKey.SetValue("", $"\"{exePath}\" \"%1\"");
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
