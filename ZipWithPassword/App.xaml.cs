using System.Windows;
using System.Windows.Threading;

namespace ZipWithPassword;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Catch any unhandled exceptions on the UI thread — prevents silent crashes
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
                    "\"Secure Starzip\" has been added to your right-click context menu.",
                    "Secure Starzip — Installed",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                ContextMenuInstaller.Uninstall();
                MessageBox.Show(
                    "\"Secure Starzip\" has been removed from your right-click context menu.",
                    "Secure Starzip — Uninstalled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }
        }

        // Normal launch — pass the first arg (source path) if provided
        string? sourcePath = e.Args.Length >= 1 ? e.Args[0] : null;
        var window = new MainWindow(sourcePath);
        window.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "Secure Starzip — Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;   // keep app alive
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        MessageBox.Show(
            $"A fatal error occurred:\n\n{ex?.Message ?? e.ExceptionObject?.ToString()}",
            "Secure Starzip — Fatal Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
