using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Starkive;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        base.OnStartup(e);

        if (e.Args.Length >= 1)
        {
            var arg = e.Args[0];

            if (arg.Equals("--install", StringComparison.OrdinalIgnoreCase))
            {
                // Runs hidden from the installer — never show dialogs, just log errors
                try { ContextMenuInstaller.Install(); }
                catch (Exception ex) { LogStartupError("--install", ex); }
                Shutdown(0);
                return;
            }

            if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                // Runs hidden from the installer — never show dialogs, just log errors
                try { ContextMenuInstaller.Uninstall(); }
                catch (Exception ex) { LogStartupError("--uninstall", ex); }
                Shutdown(0);
                return;
            }
        }

        SavedPasswordStore.Initialize();

        // Non-blocking: load cached auth token + refresh if near expiry.
        _ = AuthManager.InitializeAsync();

        // --unzip "path" — right-click "Decrypt with Starkive" on a .zip file
        if (e.Args.Length >= 2 &&
            e.Args[0].Equals("--unzip", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(e.Args[1]))
        {
            var window = new MainWindow(null);
            window.Show();
            window.OpenUnzipFile(e.Args[1]);
            return;
        }

        string? sourcePath = e.Args.Length >= 1 ? e.Args[0] : null;

        // .ssz and .zip files passed directly → open to Unzip panel
        if (sourcePath != null && File.Exists(sourcePath) &&
            (sourcePath.EndsWith(".ssz", StringComparison.OrdinalIgnoreCase) ||
             sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            var window = new MainWindow(null);
            window.Show();
            window.OpenUnzipFile(sourcePath);
            return;
        }

        var mainWindow = new MainWindow(sourcePath);
        mainWindow.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Write full details to crash log so we can diagnose without stack trace
        try
        {
            var log = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Starkive", "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log,
                $"[{DateTime.UtcNow:O}] DISPATCHER: {e.Exception}\n\n");
        }
        catch { }

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n" +
            $"Details written to:\n%APPDATA%\\Starkive\\crash.log\n\n" +
            $"Stack trace:\n{e.Exception.StackTrace}",
            "Starkive — Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void LogStartupError(string context, Exception ex)
    {
        try
        {
            var log = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Starkive", "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"[{DateTime.UtcNow:O}] {context}: {ex}\n\n");
        }
        catch { }
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Fires on any thread — never call MessageBox here. Write to crash log instead.
        try
        {
            var ex  = e.ExceptionObject as Exception;
            var msg = $"[{DateTime.UtcNow:O}] FATAL: {ex?.ToString() ?? e.ExceptionObject?.ToString()}\n";
            var log = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Starkive", "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, msg);
        }
        catch { /* last-resort handler — swallow to avoid recursive fault */ }
    }
}
