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
                ContextMenuInstaller.Install();
                MessageBox.Show(
                    "\"Starkive\" has been added to your right-click context menu.",
                    "Starkive — Installed",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                ContextMenuInstaller.Uninstall();
                MessageBox.Show(
                    "\"Starkive\" has been removed from your right-click context menu.",
                    "Starkive — Uninstalled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }
        }

        SavedPasswordStore.Initialize();

        // Non-blocking: load cached auth token + refresh if near expiry.
        _ = AuthManager.InitializeAsync();

        string? sourcePath = e.Args.Length >= 1 ? e.Args[0] : null;

        // .ssz files are opened directly — MainWindow handles the decrypt UI.
        if (sourcePath != null &&
            sourcePath.EndsWith(".ssz", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(sourcePath))
        {
            var window = new MainWindow(null);
            window.Show();
            window.OpenSszFile(sourcePath);
            return;
        }

        var mainWindow = new MainWindow(sourcePath);
        mainWindow.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "Starkive — Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
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
