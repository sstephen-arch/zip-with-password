using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ZipWithPassword;

public partial class MainWindow : Window
{
    // ── State ────────────────────────────────────────────────────────────
    private string _activeSection = "Zip";
    private AppSettings _settings;

    // ── Constructor ──────────────────────────────────────────────────────
    public MainWindow(string? sourcePath)
    {
        InitializeComponent();
        _settings = SettingsManager.Load();

        // If launched via context menu with a path, pre-fill Zip section
        if (!string.IsNullOrWhiteSpace(sourcePath) && sourcePath != "--install" && sourcePath != "--uninstall")
        {
            SetZipSource(sourcePath);
        }

        ShowSection("Zip");
        RefreshHistorySection();
        RefreshSettingsSection();
    }

    // ══════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ══════════════════════════════════════════════════════════════════════
    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            ShowSection(tag);
    }

    private void ShowSection(string section)
    {
        _activeSection = section;

        ZipSection.Visibility     = section == "Zip"      ? Visibility.Visible : Visibility.Collapsed;
        UnzipSection.Visibility   = section == "Unzip"    ? Visibility.Visible : Visibility.Collapsed;
        HistorySection.Visibility = section == "History"  ? Visibility.Visible : Visibility.Collapsed;
        SettingsSection.Visibility= section == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        // Nav highlight
        foreach (var btn in new[] { NavZip, NavUnzip, NavHistory, NavSettings })
        {
            bool active = btn.Tag?.ToString() == section;
            btn.Background = active
                ? (SolidColorBrush)FindResource("NavActiveBrush")
                : Brushes.Transparent;
            btn.Foreground = active
                ? (SolidColorBrush)FindResource("TextPrimaryBrush")
                : (SolidColorBrush)FindResource("TextSecondaryBrush");
        }

        if (section == "History")  RefreshHistorySection();
        if (section == "Settings") RefreshSettingsSection();
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZIP SECTION
    // ══════════════════════════════════════════════════════════════════════
    private void SetZipSource(string path)
    {
        ZipSourceBox.Text = path;
        ZipOutputBox.Text = BuildDefaultZipOutput(path);
    }

    private static string BuildDefaultZipOutput(string source)
    {
        source = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string dir  = Path.GetDirectoryName(source)
                   ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string name = Path.GetFileNameWithoutExtension(source);
        if (string.IsNullOrWhiteSpace(name))
            name = new DirectoryInfo(source).Name;
        string candidate = Path.Combine(dir, name + ".zip");
        int i = 1;
        while (File.Exists(candidate))
            candidate = Path.Combine(dir, $"{name} ({i++}).zip");
        return candidate;
    }

    // Drop zone
    private void DropZone_DragEnter(object sender, DragEventArgs e)  => HighlightDropZone(DropZone, true);
    private void DropZone_DragLeave(object sender, DragEventArgs e)  => HighlightDropZone(DropZone, false);
    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        HighlightDropZone(DropZone, false);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            SetZipSource(files[0]);
    }

    private void ZipBrowseSource_Click(object sender, RoutedEventArgs e)
    {
        // Try folder first via workaround using OpenFileDialog in a special mode
        var dlg = new OpenFileDialog
        {
            Title = "Select a file to zip",
            Filter = "All Files (*.*)|*.*",
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "Select Folder or File"
        };
        if (dlg.ShowDialog() == true)
        {
            string selected = dlg.FileName;
            // If user typed a folder name in the box, handle it
            if (Directory.Exists(selected))
                SetZipSource(selected);
            else if (File.Exists(selected))
                SetZipSource(selected);
            else
            {
                // They selected the placeholder — open FolderBrowserDialog equiv
                string folder = BrowseForFolder("Select folder to zip");
                if (!string.IsNullOrEmpty(folder)) SetZipSource(folder);
            }
        }
    }

    private void ZipBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save ZIP as…",
            Filter = "ZIP Archive (*.zip)|*.zip",
            DefaultExt = ".zip",
            FileName = Path.GetFileName(ZipOutputBox.Text),
            InitialDirectory = Path.GetDirectoryName(ZipOutputBox.Text)
                            ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        if (dlg.ShowDialog() == true) ZipOutputBox.Text = dlg.FileName;
    }

    // Password strength
    private void ZipPassword_Changed(object sender, RoutedEventArgs e)
    {
        ZipErrorText.Visibility = Visibility.Collapsed;
        UpdateStrengthBar();
    }

    private void UpdateStrengthBar()
    {
        string pwd = ZipPasswordBox.Password;
        int score  = ScorePassword(pwd);

        double maxWidth = StrengthBar.ActualWidth > 0
            ? ((Border)StrengthBar.Parent).ActualWidth : 200;

        StrengthBar.Width      = maxWidth * score / 4.0;
        StrengthBar.Background = score switch
        {
            0 => (SolidColorBrush)FindResource("TextSecondaryBrush"),
            1 => (SolidColorBrush)FindResource("DangerBrush"),
            2 => (SolidColorBrush)FindResource("WarningBrush"),
            3 => new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),  // blue
            _ => (SolidColorBrush)FindResource("SuccessBrush"),
        };
        StrengthLabel.Text = "Strength: " + score switch
        {
            0 => "—",
            1 => "Weak",
            2 => "Fair",
            3 => "Good",
            _ => "Strong",
        };
    }

    private static int ScorePassword(string pwd)
    {
        if (string.IsNullOrEmpty(pwd)) return 0;
        int score = 0;
        if (pwd.Length >= 8)  score++;
        if (pwd.Length >= 14) score++;
        if (pwd.Any(char.IsUpper) && pwd.Any(char.IsLower)) score++;
        if (pwd.Any(char.IsDigit) && pwd.Any(c => !char.IsLetterOrDigit(c))) score++;
        return score;
    }

    // Generate password
    private void GeneratePassword_Click(object sender, RoutedEventArgs e)
    {
        string pwd = GenerateStrongPassword(20);
        ZipPasswordBox.Password    = pwd;
        ZipConfirmBox.Password     = pwd;
        UpdateStrengthBar();

        // Show it in a friendly dialog
        var result = MessageBox.Show(
            $"Generated password:\n\n{pwd}\n\nCopy this somewhere safe — it cannot be recovered from the ZIP file.\n\nClick OK to use it, or Cancel to enter your own.",
            "Secure Starzip — Generated Password ✨",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);

        if (result != MessageBoxResult.OK)
        {
            ZipPasswordBox.Password = string.Empty;
            ZipConfirmBox.Password  = string.Empty;
            UpdateStrengthBar();
        }
    }

    public static string GenerateStrongPassword(int length = 20)
    {
        const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower   = "abcdefghjkmnpqrstuvwxyz";
        const string digits  = "23456789";
        const string special = "!@#$%^&*-_=+?";
        string pool = upper + lower + digits + special;

        var bytes = new byte[length * 2];
        RandomNumberGenerator.Fill(bytes);

        var sb = new StringBuilder(length);
        // Guarantee at least one of each category
        sb.Append(upper[bytes[0]   % upper.Length]);
        sb.Append(lower[bytes[1]   % lower.Length]);
        sb.Append(digits[bytes[2]  % digits.Length]);
        sb.Append(special[bytes[3] % special.Length]);

        for (int i = 4; i < length; i++)
            sb.Append(pool[(bytes[i] + bytes[i + length]) % pool.Length]);

        // Shuffle
        var arr = sb.ToString().ToCharArray();
        var shuffleBytes = new byte[arr.Length];
        RandomNumberGenerator.Fill(shuffleBytes);
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = shuffleBytes[i] % (i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return new string(arr);
    }

    private bool ValidateZip(out string error)
    {
        if (string.IsNullOrWhiteSpace(ZipSourceBox.Text) ||
            ZipSourceBox.Text == "No file or folder selected")
        { error = "Please select a file or folder to zip."; return false; }

        if (string.IsNullOrWhiteSpace(ZipOutputBox.Text))
        { error = "Please specify a destination path."; return false; }

        if (!ZipOutputBox.Text.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        { error = "Destination must end with .zip"; return false; }

        if (ZipPasswordBox.Password.Length == 0)
        { error = "Please enter a password."; return false; }

        if (ZipPasswordBox.Password.Length < 6)
        { error = "Password must be at least 6 characters."; return false; }

        if (ZipPasswordBox.Password != ZipConfirmBox.Password)
        { error = "Passwords do not match."; return false; }

        error = string.Empty;
        return true;
    }

    private async void ZipCreate_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateZip(out string error))
        {
            ShowZipError(error);
            return;
        }

        ZipErrorText.Visibility   = Visibility.Collapsed;
        ZipSuccessBanner.Visibility = Visibility.Collapsed;
        ZipProgressPanel.Visibility = Visibility.Visible;
        ZipCreateButton.IsEnabled   = false;

        string source   = ZipSourceBox.Text.TrimEnd('\\', '/');
        string output   = ZipOutputBox.Text;
        string password = ZipPasswordBox.Password;

        var progressWidth = ((Border)ZipProgressBar.Parent).ActualWidth;

        var prog = new Progress<(int pct, string status)>(p =>
        {
            ZipProgressBar.Width = progressWidth * p.pct / 100.0;
            ZipPercentText.Text  = $"{p.pct}%";
            ZipStatusText.Text   = p.status;
        });

        bool success = false;
        string? errMsg = null;
        try
        {
            await Task.Run(() => ZipHelper.CreateEncryptedZip(source, output, password, prog));
            success = true;
        }
        catch (Exception ex) { errMsg = ex.Message; }

        ZipProgressPanel.Visibility = Visibility.Collapsed;
        ZipCreateButton.IsEnabled   = true;

        HistoryManager.Add(new HistoryEntry
        {
            Type        = OperationType.Zip,
            SourcePath  = source,
            OutputPath  = output,
            Success     = success,
            ErrorMessage= errMsg ?? string.Empty
        });

        if (success)
            ZipSuccessBanner.Visibility = Visibility.Visible;
        else
            ShowZipError($"Error: {errMsg}");
    }

    private void ShowZipError(string msg)
    {
        ZipErrorText.Text       = msg;
        ZipErrorText.Visibility = Visibility.Visible;
    }

    // ══════════════════════════════════════════════════════════════════════
    // UNZIP SECTION
    // ══════════════════════════════════════════════════════════════════════
    private void UnzipDropZone_DragEnter(object sender, DragEventArgs e) => HighlightDropZone(UnzipDropZone, true);
    private void UnzipDropZone_DragLeave(object sender, DragEventArgs e) => HighlightDropZone(UnzipDropZone, false);

    private void UnzipDropZone_Drop(object sender, DragEventArgs e)
    {
        HighlightDropZone(UnzipDropZone, false);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            SetUnzipSource(files[0]);
    }

    private void SetUnzipSource(string path)
    {
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ShowUnzipError("Please select a .zip file.");
            return;
        }
        UnzipSourceBox.Text = path;
        UnzipOutputBox.Text = Path.Combine(
            Path.GetDirectoryName(path) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.GetFileNameWithoutExtension(path));
    }

    private void UnzipBrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select ZIP file to extract",
            Filter = "ZIP Archives (*.zip)|*.zip|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true) SetUnzipSource(dlg.FileName);
    }

    private void UnzipBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        string folder = BrowseForFolder("Select folder to extract into");
        if (!string.IsNullOrEmpty(folder)) UnzipOutputBox.Text = folder;
    }

    private async void Unzip_Click(object sender, RoutedEventArgs e)
    {
        UnzipErrorText.Visibility    = Visibility.Collapsed;
        UnzipSuccessBanner.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(UnzipSourceBox.Text) || UnzipSourceBox.Text == "No ZIP file selected")
        { ShowUnzipError("Please select a ZIP file."); return; }
        if (string.IsNullOrWhiteSpace(UnzipOutputBox.Text))
        { ShowUnzipError("Please specify an output folder."); return; }
        if (UnzipPasswordBox.Password.Length == 0)
        { ShowUnzipError("Please enter the ZIP password."); return; }

        UnzipProgressPanel.Visibility = Visibility.Visible;
        UnzipButton.IsEnabled          = false;

        string zipPath  = UnzipSourceBox.Text;
        string outFolder= UnzipOutputBox.Text;
        string password = UnzipPasswordBox.Password;

        double progWidth = ((Border)UnzipProgressBar.Parent).ActualWidth;

        var prog = new Progress<(int pct, string status)>(p =>
        {
            UnzipProgressBar.Width = progWidth * p.pct / 100.0;
            UnzipPercentText.Text  = $"{p.pct}%";
            UnzipStatusText.Text   = p.status;
        });

        bool success = false;
        string? errMsg = null;
        try
        {
            await Task.Run(() => ZipHelper.ExtractEncryptedZip(zipPath, outFolder, password, prog));
            success = true;
        }
        catch (Exception ex) { errMsg = ex.Message; }

        UnzipProgressPanel.Visibility = Visibility.Collapsed;
        UnzipButton.IsEnabled          = true;

        HistoryManager.Add(new HistoryEntry
        {
            Type        = OperationType.Unzip,
            SourcePath  = zipPath,
            OutputPath  = outFolder,
            Success     = success,
            ErrorMessage= errMsg ?? string.Empty
        });

        if (success)
            UnzipSuccessBanner.Visibility = Visibility.Visible;
        else
            ShowUnzipError(errMsg?.Contains("password", StringComparison.OrdinalIgnoreCase) == true
                ? "Wrong password or file is not encrypted."
                : $"Error: {errMsg}");
    }

    private void ShowUnzipError(string msg)
    {
        UnzipErrorText.Text       = msg;
        UnzipErrorText.Visibility = Visibility.Visible;
    }

    // ══════════════════════════════════════════════════════════════════════
    // HISTORY SECTION
    // ══════════════════════════════════════════════════════════════════════
    private void RefreshHistorySection()
    {
        var entries = HistoryManager.Load();
        var vms = entries.Select(h => new HistoryViewModel(h)).ToList();

        HistoryList.ItemsSource  = vms;
        HistoryEmpty.Visibility  = vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryCount.Text        = vms.Count == 0 ? string.Empty : $"{vms.Count} operation(s)";
    }

    private void HistoryClear_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear all history?", "Secure Starzip",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            HistoryManager.Clear();
            RefreshHistorySection();
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // SETTINGS SECTION
    // ══════════════════════════════════════════════════════════════════════
    private void RefreshSettingsSection()
    {
        DefaultFolderBox.Text = _settings.DefaultOutputFolder;

        bool installed = ContextMenuInstaller.IsInstalled();
        CtxMenuStatusText.Text = installed
            ? "Currently installed. \"Secure Starzip — Zip with password…\" appears when you right-click files and folders."
            : "Not installed. Click the button to add Secure Starzip to your right-click menu (requires admin).";
        CtxMenuButton.Content = installed ? "Uninstall" : "Install";
    }

    private void SettingsBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        string folder = BrowseForFolder("Select default output folder");
        if (!string.IsNullOrEmpty(folder)) DefaultFolderBox.Text = folder;
    }

    private void CtxMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ContextMenuInstaller.IsInstalled())
                ContextMenuInstaller.Uninstall();
            else
                ContextMenuInstaller.Install();
            RefreshSettingsSection();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not update context menu:\n{ex.Message}\n\nTry running Secure Starzip as Administrator.",
                "Secure Starzip", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _settings.DefaultOutputFolder = DefaultFolderBox.Text;
        SettingsManager.Save(_settings);
        SettingsSavedBanner.Visibility = Visibility.Visible;
    }

    // ══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════════════
    private static void HighlightDropZone(Border zone, bool on)
    {
        zone.BorderBrush = on
            ? new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1))  // accent
            : new SolidColorBrush(Color.FromRgb(0x25, 0x2B, 0x3B));
        zone.Background = on
            ? new SolidColorBrush(Color.FromArgb(0x18, 0x63, 0x66, 0xF1))
            : new SolidColorBrush(Color.FromRgb(0x1B, 0x1F, 0x2E));
    }

    private static string BrowseForFolder(string description)
    {
        // Use OpenFileDialog trick for folder picking (no dependency on WinForms)
        var dlg = new OpenFileDialog
        {
            Title            = description,
            CheckFileExists  = false,
            CheckPathExists  = true,
            FileName         = "Select Folder",
            Filter           = "Folders|*.none",
            ValidateNames    = false
        };
        if (dlg.ShowDialog() == true)
            return Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
        return string.Empty;
    }
}

// ── View-model for history items ─────────────────────────────────────────
public class HistoryViewModel
{
    private readonly HistoryEntry _entry;
    public HistoryViewModel(HistoryEntry entry) => _entry = entry;

    public string TypeIcon     => _entry.Type == OperationType.Zip ? "🔒" : "📂";
    public string SourcePath   => _entry.SourcePath;
    public string OutputPath   => _entry.OutputPath;
    public string FormattedTime => _entry.Timestamp.ToString("MMM d, h:mm tt");
}
