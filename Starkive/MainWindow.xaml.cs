using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Starkive;

public partial class MainWindow : Window
{
    // ─── State ───────────────────────────────────────────────────────────────
    private string      _activeSection   = "Home";
    private AppSettings _settings;
    private bool        _sidebarExpanded = true;
    private string      _pwdMode         = "Password";   // Password | Passphrase
    private bool        _autoGenVisible  = false;
    private string      _activePassword  = string.Empty;
    private int         _wordCount       = 4;
    private string      _separator       = "·";
    private bool        _isSszMode                  = false;
    private string      _saveDest                   = "Local"; // "Local" | "GoogleDrive" | "OneDrive"
    private bool        _sszCreatedWhileLoggedOut   = false;
    private string      _lastStarName               = "";
    private bool        _suppressThemeChange        = true;   // true until constructor finishes init
    private DispatcherTimer? _toastTimer;

    // ─── Wordlist ─────────────────────────────────────────────────────────────
    private static string[]? _wordlist;

    private static string[] GetWordlist()
    {
        if (_wordlist != null) return _wordlist;
        try
        {
            var asm    = Assembly.GetExecutingAssembly();
            var name   = asm.GetManifestResourceNames()
                            .FirstOrDefault(n => n.EndsWith("eff_wordlist.txt"));
            if (name != null)
            {
                using var stream = asm.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                _wordlist = reader.ReadToEnd()
                    .Split('\n')
                    .Where(l => l.Contains('\t'))
                    .Select(l => l.Split('\t').LastOrDefault()?.Trim())
                    .Where(w => !string.IsNullOrEmpty(w))
                    .Select(w => w!)
                    .ToArray();
            }
        }
        catch { }
        _wordlist ??= ["maple","stone","river","cloud","north","frost","ember","grove"];
        return _wordlist;
    }

    // ─── Constructor ─────────────────────────────────────────────────────────
    public MainWindow(string? sourcePath)
    {
        InitializeComponent();
        _settings = SettingsManager.Load();

        // One-time migration: versions before 1.4.0 defaulted to Dark theme.
        // Reset to Light on first launch of 1.4.0+ so existing users get the new look.
        if (_settings.LastSeenVersion != AppConstants.AppVersion && _settings.Theme == "Dark")
        {
            _settings.Theme = "Light";
        }
        _settings.LastSeenVersion = AppConstants.AppVersion;
        SettingsManager.Save(_settings);

        // Restore sidebar state
        _sidebarExpanded = !_settings.SidebarCollapsed;
        if (!_sidebarExpanded) ApplySidebarState(animate: false);

        // Pre-load wordlist off startup thread
        Task.Run(() => GetWordlist());

        if (!string.IsNullOrWhiteSpace(sourcePath)
            && sourcePath != "--install"
            && sourcePath != "--uninstall")
        {
            if (sourcePath.EndsWith(".ssz", StringComparison.OrdinalIgnoreCase))
                OpenSszFile(sourcePath);
            else
                SetZipSource(sourcePath);
        }

        VersionText.Text = $"v{AppConstants.AppVersion}";
        // Apply theme FIRST so nav brush snapshots use the correct theme colors
        _suppressThemeChange = true;
        ThemeComboBox.SelectedIndex = _settings.Theme switch
        {
            "Light"    => 1,
            "Titanium" => 2,
            "System"   => 3,
            _          => 0,   // Dark (default)
        };
        _suppressThemeChange = false;
        ApplyTheme(_settings.Theme);

        // Dynamic greeting
        var hour = DateTime.Now.Hour;
        HomeGreeting.Text = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
        ShowSection("Home");
        SetPasswordMode("Password");
        SelectSegment(BtnModePassword, new[] { BtnModePassword, BtnModePassphrase });
        SelectSegment(BtnFmtZip,      new[] { BtnFmtZip, BtnFmtSsz });
        SelectSegment(Btn4Words,       new[] { Btn3Words, Btn4Words, Btn5Words });
        SelectSegment(BtnSepDot,       new[] { BtnSepDot, BtnSepDash, BtnSepSpace });
        SelectSegment(BtnPhraseGenerate, new[] { BtnPhraseGenerate, BtnPhraseOwn });
        RegeneratePassphrase();
        RegenerateAutoPassword();
        RefreshHistorySection();
        RefreshHomeSection();
        RefreshSettingsSection();
        UpdateSaveDestUI(); // init tile styles on startup

        RefreshProStatus();

        // Non-blocking startup version check.
        _ = CheckForUpdateAsync();
    }

    private string? _updateUrl;
    private string? _updateVersion;

    private async Task CheckForUpdateAsync()
    {
        try
        {
            await Task.Delay(3000);
            AppLog.Write($"UpdateCheck: current={AppConstants.AppVersion}");
            var (latest, downloadUrl) = await ApiService.GetLatestReleaseAsync();
            AppLog.Write($"UpdateCheck: latest={latest ?? "null"} url={downloadUrl ?? "null"}");
            if (string.IsNullOrEmpty(latest) || string.IsNullOrEmpty(downloadUrl)) return;

            if (Version.TryParse(latest, out var latestVer) &&
                Version.TryParse(AppConstants.AppVersion, out var currentVer) &&
                latestVer > currentVer)
            {
                _updateUrl     = downloadUrl;
                _updateVersion = latest;
                AppLog.Write($"UpdateCheck: showing banner for {latest}");
                Dispatcher.Invoke(() =>
                {
                    UpdateBannerText.Text     = $"⬆  Starkive {latest} is available — click Install Update (takes ~30 sec).";
                    UpdateDownloadBtn.Content = "Install Update";
                    UpdateBanner.Visibility   = Visibility.Visible;
                });
            }
            else
            {
                AppLog.Write($"UpdateCheck: already up to date ({AppConstants.AppVersion} >= {latest})");
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"UpdateCheck error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void UpdateDownload_Click(object sender, RoutedEventArgs e)
    {
        if (_updateUrl != null)
            _ = AutoInstallUpdateAsync(_updateUrl, _updateVersion ?? "");
    }

    private async Task AutoInstallUpdateAsync(string downloadUrl, string version)
    {
        UpdateDownloadBtn.IsEnabled = false;

        try
        {
            // ── 1. Download ──────────────────────────────────────────────────
            UpdateBannerText.Text = "Downloading update…";
            string tempPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"StarkiveSetup-{version}.exe");

            using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                var bytes = await http.GetByteArrayAsync(downloadUrl);
                await System.IO.File.WriteAllBytesAsync(tempPath, bytes);
            }

            // ── 2. Run installer silently then exit ──────────────────────────
            // /VERYSILENT = no UI at all; /NORESTART = don't reboot Windows
            // Inno Setup's CloseApplications=yes will close this process before
            // overwriting the exe, then relaunch the new version automatically.
            UpdateBannerText.Text = "Installing…";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath,
                "/VERYSILENT /NORESTART /CLOSEAPPLICATIONS")
            {
                UseShellExecute = true,
                Verb            = "runas",   // request elevation for Program Files write
            });

            // Give the installer a moment to start, then shut down this instance
            await Task.Delay(1500);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            AppLog.Write($"Auto-update failed: {ex.Message}");
            UpdateBannerText.Text     = "Download failed — check your connection and try again.";
            UpdateDownloadBtn.Content  = "Retry";
            UpdateDownloadBtn.IsEnabled = true;
        }
    }

    private void UpdateDismiss_Click(object sender, RoutedEventArgs e)
        => UpdateBanner.Visibility = Visibility.Collapsed;

    // ─── Navigation ──────────────────────────────────────────────────────────
    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            ShowSection(tag);
    }

    private void ShowSection(string section)
    {
        _activeSection = section;

        HomeSection.Visibility     = section == "Home"     ? Visibility.Visible : Visibility.Collapsed;
        ZipSection.Visibility      = section == "Zip"      ? Visibility.Visible : Visibility.Collapsed;
        UnzipSection.Visibility    = section == "Unzip"    ? Visibility.Visible : Visibility.Collapsed;
        HistorySection.Visibility  = section == "History"  ? Visibility.Visible : Visibility.Collapsed;
        VaultSection.Visibility    = section == "Vault"    ? Visibility.Visible : Visibility.Collapsed;
        SettingsSection.Visibility = section == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        // Nav items + accent bars
        var navItems = new[]
        {
            (btn: NavHome,     accent: NavHomeAccent,     tag: "Home"),
            (btn: NavZip,      accent: NavZipAccent,      tag: "Zip"),
            (btn: NavUnzip,    accent: NavUnzipAccent,    tag: "Unzip"),
            (btn: NavHistory,  accent: NavHistoryAccent,  tag: "History"),
            (btn: NavSettings, accent: NavSettingsAccent, tag: "Settings"),
            (btn: NavVault,    accent: NavVaultAccent,    tag: "Vault"),
        };

        var accentColor = (SolidColorBrush)FindResource("AccentBrush");
        var navActiveBg = (SolidColorBrush)FindResource("NavActiveBgBrush");

        foreach (var (btn, accent, tag) in navItems)
        {
            bool active = tag == section;
            btn.Background = active ? navActiveBg : Brushes.Transparent;
            btn.Foreground = active
                ? (SolidColorBrush)FindResource("TextPrimaryBrush")
                : (SolidColorBrush)FindResource("TextSecondaryBrush");
            accent.Fill = active ? accentColor : Brushes.Transparent;
        }

        if (section == "History")  RefreshHistorySection();
        if (section == "Settings") RefreshSettingsSection();
        if (section == "Home")     RefreshHomeSection();
        if (section == "Vault")    RefreshVaultSection();

        // Reset success screen when leaving Zip section
        if (section != "Zip")
        {
            ZipSuccessPanel.Visibility = Visibility.Collapsed;
            ZipFormPanel.Visibility    = Visibility.Visible;
        }
    }

    // ─── Sidebar collapse/expand ─────────────────────────────────────────────
    private void Hamburger_Click(object sender, RoutedEventArgs e)
    {
        _sidebarExpanded = !_sidebarExpanded;
        ApplySidebarState(animate: true);
        _settings.SidebarCollapsed = !_sidebarExpanded;
        SettingsManager.Save(_settings);
    }

    private void ApplySidebarState(bool animate)
    {
        double target = _sidebarExpanded ? 200 : 52;

        if (animate)
        {
            var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            SidebarBorder.BeginAnimation(WidthProperty, anim);
        }
        else
        {
            SidebarBorder.Width = target;
        }

        var textVis = _sidebarExpanded ? Visibility.Visible : Visibility.Collapsed;
        LogoTextPanel.Visibility     = textVis;
        NavHomeText.Visibility       = textVis;
        NavZipText.Visibility        = textVis;
        NavUnzipText.Visibility      = textVis;
        NavHistoryText.Visibility    = textVis;
        NavSettingsText.Visibility   = textVis;
        NavVaultText.Visibility      = textVis;
        VersionText.Visibility       = textVis;
        GoProLabel.Visibility        = textVis;
        GoProPrice.Visibility        = textVis;

        LogoMark.HorizontalAlignment = _sidebarExpanded
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Center;
    }

    // ─── Home section ─────────────────────────────────────────────────────────
    private void RefreshHomeSection()
    {
        var entries = HistoryManager.Load();
        var recent  = entries.Take(5).Select(h => new HistoryViewModel(h)).ToList();
        HomeRecentList.ItemsSource = recent;
        HomeRecentEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HomeCardZip_Click(object sender, MouseButtonEventArgs e)
        => ShowSection("Zip");

    private void HomeCardUnzip_Click(object sender, MouseButtonEventArgs e)
        => ShowSection("Unzip");

    // Button variant (RoutedEventHandler) for hero CTA and Pro upsell button
    private async void HomeCardPro_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthManager.IsLoggedIn)
        {
            var dlg = new OtpDialog { Owner = this };
            dlg.ShowDialog();
            RefreshProStatus();
        }
        if (!AuthManager.IsLoggedIn || AuthManager.IsProUser) return;

        await StartCheckoutAsync();
    }

    private void HomeCardPro_Click(object sender, MouseButtonEventArgs e)
    {
        // Navigate to Zip & Encrypt with Secure Container pre-selected
        ShowSection("Zip");
        if (AuthManager.IsProUser)
        {
            // Directly activate SSZ mode — user is already Pro
            _isSszMode = true;
            SelectSegment(BtnFmtSsz, new[] { BtnFmtZip, BtnFmtSsz });
            RecipientHintPanel.Visibility = Visibility.Visible;
            ZipCreateButton.Content = "Create Secure Container";
            string current = ZipOutputBox.Text;
            if (current.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                ZipOutputBox.Text = Path.ChangeExtension(current, ".ssz");
        }
        else
        {
            // Prompt login/upgrade, then activate SSZ if they become Pro
            if (!RequirePro("Secure Container (.ssz)"))
            {
                ShowSection("Zip");
                SelectSegment(BtnFmtSsz, new[] { BtnFmtZip, BtnFmtSsz });
                RecipientHintPanel.Visibility = Visibility.Visible;
                ZipCreateButton.Content = "Create Secure Container";
            }
        }
    }

    private void HomeCardHistory_Click(object sender, MouseButtonEventArgs e)
        => ShowSection("History");

    private void HomeDropZone_DragEnter(object sender, DragEventArgs e)
    {
        HomeDropRect.Stroke = new SolidColorBrush(Color.FromRgb(0x1A, 0x56, 0xDB));
        ((Border)sender).Background = new SolidColorBrush(Color.FromArgb(0x18, 0x1A, 0x56, 0xDB));
    }

    private void HomeDropZone_DragLeave(object sender, DragEventArgs e)
    {
        HomeDropRect.Stroke = (Brush)FindResource("BorderBrush");
        ((Border)sender).Background = Brushes.Transparent;
    }

    private void HomeDropZone_Drop(object sender, DragEventArgs e)
    {
        HomeDropRect.Stroke = (Brush)FindResource("BorderBrush");
        ((Border)sender).Background = Brushes.Transparent;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            SetZipSource(files[0]);
            ShowSection("Zip");
        }
    }

    private void GoProBtn_Click(object sender, MouseButtonEventArgs e)
        => OpenAuthDialog();

    private async void UpgradeToPro_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthManager.IsLoggedIn)
        {
            var dlg = new OtpDialog { Owner = this };
            dlg.ShowDialog();
            RefreshProStatus();
        }
        if (!AuthManager.IsLoggedIn || AuthManager.IsProUser) return;

        await StartCheckoutAsync();
    }

    private async Task StartCheckoutAsync()
    {
        string email = AuthManager.UserEmail ?? "";
        string? url = null;

        if (!string.IsNullOrEmpty(email))
            url = await ApiService.CreateCheckoutSessionAsync(email);

        // Fall back to pricing page if session creation fails
        OpenUrl(url ?? "https://starkive.app/#pricing");

        // Poll for Pro status in the background — update UI the moment payment lands
        _ = PollForProAsync();
    }

    private async Task PollForProAsync()
    {
        // Poll every 5 seconds for up to 3 minutes
        for (int i = 0; i < 36; i++)
        {
            await Task.Delay(5000);
            bool isPro = await ApiService.FetchProStatusAsync();
            if (!isPro) continue;

            AuthManager.SetProStatus(true);
            Dispatcher.Invoke(() =>
            {
                RefreshProStatus();
                ShowToast("You're now Pro! All features unlocked.");
            });
            return;
        }
    }

    private void ViewDashboard_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://starkive.app/dashboard");

    /// <summary>Opens a URL in the default browser; copies it on failure.</summary>
    private void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLog.Write($"OpenUrl error: {ex.Message}");
            Clipboard.SetText(url);
            ShowToast("Could not open browser — link copied to clipboard.");
        }
    }

    private async void OpenAuthDialog()
    {
        if (AuthManager.IsLoggedIn)
        {
            if (!AuthManager.IsProUser)
                await StartCheckoutAsync();
            return;
        }
        var dlg = new OtpDialog { Owner = this };
        dlg.ShowDialog();
        RefreshProStatus();
        if (AuthManager.IsLoggedIn && !AuthManager.IsProUser)
            await StartCheckoutAsync();
    }

    // ─── ZIP source helpers ───────────────────────────────────────────────────
    private void SetZipSource(string path)
    {
        // Clear results from any previous run
        ZipSuccessBanner.Visibility = Visibility.Collapsed;
        ZipErrorText.Visibility     = Visibility.Collapsed;

        ZipSourceBox.Text = path;
        ZipOutputBox.Text = BuildDefaultZipOutput(path);
    }

    private string BuildDefaultZipOutput(string source)
    {
        source = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string dir = Path.GetDirectoryName(source)
                  ?? _settings.DefaultOutputFolder
                  ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string name = Path.GetFileNameWithoutExtension(source);
        if (string.IsNullOrWhiteSpace(name))
            name = new DirectoryInfo(source).Name;
        string ext = _isSszMode ? ".ssz" : ".zip";
        string candidate = Path.Combine(dir, name + ext);
        int i = 1;
        while (File.Exists(candidate))
            candidate = Path.Combine(dir, $"{name} ({i++}){ext}");
        return candidate;
    }

    internal void OpenSszFile(string sszPath) => OpenUnzipFile(sszPath);

    internal void OpenUnzipFile(string path)
    {
        ShowSection("Unzip");
        SetUnzipSource(path);
    }

    // ─── Drop zone (Zip panel) ────────────────────────────────────────────────
    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        ZipDropRect.Stroke = new SolidColorBrush(Color.FromRgb(0x1A, 0x56, 0xDB));
        DropZone.Background = new SolidColorBrush(Color.FromArgb(0x18, 0x1A, 0x56, 0xDB));
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        ZipDropRect.Stroke = (Brush)FindResource("BorderBrush");
        DropZone.Background = Brushes.Transparent;
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        ZipDropRect.Stroke = (Brush)FindResource("BorderBrush");
        DropZone.Background = Brushes.Transparent;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            SetZipSource(files[0]);
    }

    private void UnzipDropZone_DragEnter(object sender, DragEventArgs e)
        => HighlightDropZone(UnzipDropZone, true);

    private void UnzipDropZone_DragLeave(object sender, DragEventArgs e)
        => HighlightDropZone(UnzipDropZone, false);

    private void UnzipDropZone_Drop(object sender, DragEventArgs e)
    {
        HighlightDropZone(UnzipDropZone, false);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            SetUnzipSource(files[0]);
    }

    private static void HighlightDropZone(Border zone, bool on)
    {
        zone.BorderBrush = on
            ? new SolidColorBrush(Color.FromRgb(0x1A, 0x56, 0xDB))
            : new SolidColorBrush(Color.FromRgb(0x21, 0x26, 0x2D));
        zone.Background = on
            ? new SolidColorBrush(Color.FromArgb(0x18, 0x1A, 0x56, 0xDB))
            : Brushes.Transparent;
    }

    // ─── Browse dialogs ───────────────────────────────────────────────────────
    private void ZipBrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select a file to zip",
            Filter = "All Files (*.*)|*.*",
            CheckFileExists = false, CheckPathExists = true,
            FileName = "Select Folder or File"
        };
        if (dlg.ShowDialog() == true)
        {
            string selected = dlg.FileName;
            if      (Directory.Exists(selected)) SetZipSource(selected);
            else if (File.Exists(selected))      SetZipSource(selected);
            else
            {
                string folder = BrowseForFolder("Select folder to zip");
                if (!string.IsNullOrEmpty(folder)) SetZipSource(folder);
            }
        }
    }

    private void ZipBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        bool isSsz = _isSszMode;
        var dlg = new SaveFileDialog
        {
            Title      = isSsz ? "Save Secure Container as..." : "Save ZIP as...",
            Filter     = isSsz ? "Starkive Secure Container (*.ssz)|*.ssz"
                                : "ZIP Archive (*.zip)|*.zip",
            DefaultExt = isSsz ? ".ssz" : ".zip",
            FileName   = Path.GetFileName(ZipOutputBox.Text),
            InitialDirectory = Path.GetDirectoryName(ZipOutputBox.Text)
                            ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        if (dlg.ShowDialog() == true) ZipOutputBox.Text = dlg.FileName;
    }

    // ─── Password mode selector ───────────────────────────────────────────────
    private void PwdMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string mode)
        {
            SetPasswordMode(mode);
            SelectSegment(btn, new[] { BtnModePassword, BtnModePassphrase });
        }
    }

    private void SetPasswordMode(string mode)
    {
        _pwdMode = mode;
        PwdPanelPassword.Visibility   = mode == "Password"   ? Visibility.Visible : Visibility.Collapsed;
        PwdPanelPassphrase.Visibility = mode == "Passphrase" ? Visibility.Visible : Visibility.Collapsed;
        WeakPasswordWarning.Visibility = Visibility.Collapsed;
        // Collapse auto-gen panel when switching away from Password mode
        if (mode != "Password") { _autoGenVisible = false; AutoGenPanel.Visibility = Visibility.Collapsed; }
    }

    // ─── Inline auto-generate (Password panel) ────────────────────────────────
    private void AutoGenToggle_Click(object sender, RoutedEventArgs e)
    {
        _autoGenVisible = !_autoGenVisible;
        AutoGenPanel.Visibility = _autoGenVisible ? Visibility.Visible : Visibility.Collapsed;
        if (_autoGenVisible && string.IsNullOrEmpty(AutoGenDisplay.Text))
            RegenerateAutoPassword();
    }

    private void UseAutoPassword_Click(object sender, RoutedEventArgs e)
    {
        string pwd = AutoGenDisplay.Text;
        if (string.IsNullOrEmpty(pwd)) return;
        // Copy generated password into the manual fields so validation passes normally
        ZipPasswordBox.Password  = pwd;
        ZipConfirmBox.Password   = pwd;
        AutoGenPanel.Visibility  = Visibility.Collapsed;
        _autoGenVisible          = false;
        UpdateStrengthBar();
        ShowToast("Password applied");
    }

    // ─── Segmented control helper ─────────────────────────────────────────────
    private void SelectSegment(Button selected, Button[] all)
    {
        foreach (var btn in all)
        {
            bool active = btn == selected;
            btn.Background = active
                ? (SolidColorBrush)FindResource("AccentBrush")
                : Brushes.Transparent;
            btn.Foreground = active
                ? Brushes.White
                : (SolidColorBrush)FindResource("TextSecondaryBrush");
        }
    }

    // ─── Passphrase mode ─────────────────────────────────────────────────────
    private void WordCount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int wc))
        {
            _wordCount = wc;
            SelectSegment(btn, new[] { Btn3Words, Btn4Words, Btn5Words });
            RegeneratePassphrase();
        }
    }

    private void Separator_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sep)
        {
            _separator = sep;
            SelectSegment(btn, new[] { BtnSepDot, BtnSepDash, BtnSepSpace });
            RegeneratePassphrase();
        }
    }

    private void RegeneratePassphrase_Click(object sender, RoutedEventArgs e)
        => RegeneratePassphrase();

    private void RegeneratePassphrase()
    {
        var words = GetWordlist();
        if (words.Length == 0) return;

        var selected = new string[_wordCount];
        var buf      = new byte[4];
        for (int i = 0; i < _wordCount; i++)
        {
            RandomNumberGenerator.Fill(buf);
            uint idx = BitConverter.ToUInt32(buf, 0) % (uint)words.Length;
            selected[i] = words[idx];
        }
        string phrase           = string.Join(_separator, selected);
        PassphraseDisplay.Text  = phrase;
        _activePassword         = phrase;
    }

    private void CopyPassphrase_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(PassphraseDisplay.Text))
        {
            Clipboard.SetText(PassphraseDisplay.Text);
            ShowToast("Copied to clipboard");
        }
    }

    // ─── Auto-generate mode ───────────────────────────────────────────────────
    private void AutoGenOption_Changed(object sender, RoutedEventArgs e)
    {
        bool anyChecked = (ChkUpper.IsChecked == true)
                       || (ChkLower.IsChecked == true)
                       || (ChkDigits.IsChecked == true)
                       || (ChkSpecial.IsChecked == true);
        if (!anyChecked && sender is CheckBox cb)
            cb.IsChecked = true;
        RegenerateAutoPassword();
    }

    private void LengthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LengthLabel == null) return;
        int len            = (int)LengthSlider.Value;
        LengthLabel.Text   = $"{len} characters";
        RegenerateAutoPassword();
    }

    private void RegenerateAutoPassword_Click(object sender, RoutedEventArgs e)
        => RegenerateAutoPassword();

    private void RegenerateAutoPassword()
    {
        if (AutoGenDisplay == null || LengthSlider == null) return;

        int    len  = (int)LengthSlider.Value;
        string pwd  = PasswordGenerator.Generate(len,
            useUpper:   ChkUpper.IsChecked == true,
            useLower:   ChkLower.IsChecked == true,
            useDigits:  ChkDigits.IsChecked == true,
            useSpecial: ChkSpecial.IsChecked == true);
        AutoGenDisplay.Text = pwd;
        _activePassword = pwd;
    }

    private void CopyAutoPassword_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(AutoGenDisplay.Text))
        {
            Clipboard.SetText(AutoGenDisplay.Text);
            ShowToast("Copied to clipboard");
        }
    }


    // ─── Password strength ────────────────────────────────────────────────────
    private void ZipPassword_Changed(object sender, RoutedEventArgs e)
    {
        ZipErrorText.Visibility = Visibility.Collapsed;
        UpdateStrengthBar();
    }

    private void UpdateStrengthBar()
    {
        string pwd  = ZipPasswordBox.Password;
        int    score = ScorePassword(pwd);
        double maxW  = ((Border)StrengthBar.Parent).ActualWidth;
        if (maxW <= 0) maxW = 300;

        StrengthBar.Width = maxW * score / 4.0;
        StrengthBar.Background = score switch
        {
            0 => new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
            1 => (SolidColorBrush)FindResource("DangerBrush"),
            2 => (SolidColorBrush)FindResource("WarningBrush"),
            3 => new SolidColorBrush(Color.FromRgb(0x1A, 0x56, 0xDB)),
            _ => (SolidColorBrush)FindResource("SuccessBrush"),
        };

        string label = score switch { 0 => "--", 1 => "Weak", 2 => "Fair", 3 => "Good", _ => "Strong" };
        StrengthLabel.Text       = label;
        StrengthLabel.Foreground = StrengthBar.Background;

        int charsetSize = 0;
        if (pwd.Any(char.IsLower))                  charsetSize += 26;
        if (pwd.Any(char.IsUpper))                  charsetSize += 26;
        if (pwd.Any(char.IsDigit))                  charsetSize += 10;
        if (pwd.Any(c => !char.IsLetterOrDigit(c))) charsetSize += 32;
        if (charsetSize > 0 && pwd.Length > 0)
        {
            double bits = pwd.Length * Math.Log2(charsetSize);
            EntropyLabel.Text = $"~{bits:F0} bits of entropy · {pwd.Length} characters";
        }
        else
        {
            EntropyLabel.Text = string.Empty;
        }

        // Show brute-force risk warning when the user has typed a weak manual password.
        WeakPasswordWarning.Visibility =
            (_pwdMode == "Password" && score <= 1 && pwd.Length > 0)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    // Returns true and shows the auth dialog if the user is not Pro.
    // Call at the entry point of any Pro-only feature before doing work.
    private bool RequirePro(string featureName)
    {
        if (AuthManager.IsProUser) return false;

        if (!AuthManager.IsLoggedIn)
        {
            var dlg = new OtpDialog { Owner = this };
            dlg.ShowDialog();
            RefreshProStatus();
            if (AuthManager.IsProUser) return false;
        }

        // Logged in but not Pro — go straight to checkout
        ShowToast($"{featureName} requires Starkive Pro. Opening checkout...");
        _ = StartCheckoutAsync();
        return true; // blocked
    }

    private static int ScorePassword(string pwd)
    {
        if (string.IsNullOrEmpty(pwd)) return 0;
        int score = 0;
        if (pwd.Length >= 8)  score++;
        if (pwd.Length >= 12 && (pwd.Any(char.IsUpper) || pwd.Any(char.IsLower))) score++;
        if (pwd.Length >= 12 && pwd.Any(char.IsUpper) && pwd.Any(char.IsLower) && pwd.Any(char.IsDigit)) score++;
        if (pwd.Length >= 14 && pwd.Any(char.IsUpper) && pwd.Any(char.IsLower)
            && pwd.Any(char.IsDigit) && pwd.Any(c => !char.IsLetterOrDigit(c))) score++;
        return score;
    }

    // ─── Zip create ───────────────────────────────────────────────────────────
    private bool ValidateZip(out string error)
    {
        if (string.IsNullOrWhiteSpace(ZipSourceBox.Text) || ZipSourceBox.Text == "No file or folder selected")
        { error = "Please select a file or folder to zip."; return false; }
        if (string.IsNullOrWhiteSpace(ZipOutputBox.Text))
        { error = "Please specify a destination path."; return false; }
        string requiredExt = _isSszMode ? ".ssz" : ".zip";
        if (!ZipOutputBox.Text.EndsWith(requiredExt, StringComparison.OrdinalIgnoreCase))
        { error = $"Destination must end with {requiredExt}"; return false; }

        string pwd = ResolveActivePassword();
        if (string.IsNullOrEmpty(pwd))
        { error = "Please enter or generate a password."; return false; }
        if (_pwdMode == "Password" && pwd.Length < 6)
        { error = "Password must be at least 6 characters."; return false; }
        if (_pwdMode == "Password" && pwd != ZipConfirmBox.Password)
        { error = "Passwords do not match."; return false; }
        if (_pwdMode == "Passphrase" && _phraseSubMode == "Own" && pwd != OwnPassphraseConfirmBox.Text)
        { error = "Passphrases do not match."; return false; }

        error = string.Empty;
        return true;
    }

    private string _phraseSubMode = "Generate"; // Generate | Own

    private void PhraseSubMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string mode)
        {
            _phraseSubMode = mode;
            SelectSegment(btn, new[] { BtnPhraseGenerate, BtnPhraseOwn });
            PhraseGeneratePanel.Visibility = mode == "Generate" ? Visibility.Visible : Visibility.Collapsed;
            PhraseOwnPanel.Visibility      = mode == "Own"      ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OwnPassphrase_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        bool mismatch = OwnPassphraseBox.Text != OwnPassphraseConfirmBox.Text
                        && OwnPassphraseConfirmBox.Text.Length > 0;
        OwnPassphraseMismatch.Visibility = mismatch ? Visibility.Visible : Visibility.Collapsed;
    }

    private string ResolveActivePassword()
    {
        if (_pwdMode == "Passphrase")
            return _phraseSubMode == "Own" ? OwnPassphraseBox.Text : PassphraseDisplay.Text;
        return ZipPasswordBox.Password;
    }

    private void OutputFmt_Click(object sender, RoutedEventArgs e)
    {
        string tag = (string)((Button)sender).Tag;
        bool ssz = tag == "SSZ";

        if (ssz && RequirePro("Certified Delivery (.ssz)")) return;

        _isSszMode = ssz;

        // Segment button highlight
        SelectSegment(ssz ? BtnFmtSsz : BtnFmtZip, new[] { BtnFmtZip, BtnFmtSsz });

        // Show/hide Certified Delivery info card and recipient hint
        CertifiedDeliveryInfoCard.Visibility = ssz ? Visibility.Visible : Visibility.Collapsed;
        RecipientHintPanel.Visibility        = ssz ? Visibility.Visible : Visibility.Collapsed;

        // Update create button label and output extension
        ZipCreateButton.Content = ssz ? "Create Certified Delivery" : "Create Encrypted ZIP";

        // Show/hide cloud destination buttons (only relevant for SSZ)
        RefreshSaveDestButtons();

        // Switch output extension
        string current = ZipOutputBox.Text;
        if (ssz && current.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ZipOutputBox.Text = Path.ChangeExtension(current, ".ssz");
        else if (!ssz && current.EndsWith(".ssz", StringComparison.OrdinalIgnoreCase))
            ZipOutputBox.Text = Path.ChangeExtension(current, ".zip");
    }

    private async void ZipCreate_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateZip(out string error)) { ShowZipError(error); return; }

        ZipErrorText.Visibility     = Visibility.Collapsed;
        ZipSuccessBanner.Visibility = Visibility.Collapsed;
        ZipProgressPanel.Visibility = Visibility.Visible;
        ZipCreateButton.IsEnabled   = false;

        string source   = ZipSourceBox.Text.TrimEnd('\\', '/');
        // For cloud destinations use a temp path; for local use the box value
        bool   toCloud  = _isSszMode && _saveDest != "Local";
        string output   = toCloud
            ? Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(ZipOutputBox.Text) + ".ssz")
            : ZipOutputBox.Text;
        string password = ResolveActivePassword();
        double progWidth = ((Border)ZipProgressBar.Parent).ActualWidth;

        var prog = new Progress<(int pct, string status)>(p =>
        {
            ZipProgressBar.Width = progWidth * p.pct / 100.0;
            ZipPercentText.Text  = $"{p.pct}%";
            ZipStatusText.Text   = p.status;
        });

        bool success = false; string? errMsg = null;

        if (_isSszMode)
        {
            string recipientHint = RecipientHintBox.Text.Trim();
            try
            {
                var result = await Task.Run(() => SszHelper.Create(source, output, password, prog));
                success = true;
                output = result.FinalOutputPath;
                _lastStarName = result.StarName;
                // The star name is appended to the filename — show the real path
                // so users know what to look for on disk.
                if (!toCloud) ZipOutputBox.Text = output;
                if (AuthManager.IsLoggedIn)
                {
                    // Await registration so we know if it succeeded before showing success
                    await ApiService.RegisterSszFileAsync(new SszFileRecord
                    {
                        OwnerId          = AuthManager.UserId ?? "",
                        FileToken        = result.FileToken,
                        OriginalFilename = Path.GetFileName(output),
                        FileSizeBytes    = new FileInfo(output).Length,
                        Sha256Hash       = Convert.ToHexString(result.PayloadHash).ToLowerInvariant(),
                        RecipientHint    = string.IsNullOrEmpty(recipientHint) ? null : recipientHint,
                        StarName         = result.StarName,
                    });
                    AppLog.Write($"SSZ created & registered: {Path.GetFileName(output)}, star={result.StarName}, token={result.FileToken}");
                }
                else
                {
                    AppLog.Write($"SSZ created WITHOUT registration (not logged in): {Path.GetFileName(output)}");
                    _sszCreatedWhileLoggedOut = true;
                }
            }
            catch (Exception ex) { errMsg = ex.Message; }
        }
        else
        {
            try { await Task.Run(() => ZipHelper.CreateEncryptedZip(source, output, password, prog)); success = true; }
            catch (Exception ex) { errMsg = ex.Message; }
        }

        ZipProgressPanel.Visibility = Visibility.Collapsed;
        ZipCreateButton.IsEnabled   = true;
        HistoryManager.Add(new HistoryEntry
        {
            Type = OperationType.Zip, SourcePath = source, OutputPath = output,
            Success = success, ErrorMessage = errMsg ?? string.Empty
        });

        if (success)
        {
            _lastCreatedSszPath = _isSszMode ? output : null;

            // Headline
            if (_isSszMode && _sszCreatedWhileLoggedOut)
            {
                ZipSuccessHeadline.Text  = $"Certified Delivery created!";
                ZipSuccessSubtitle.Text  = $"\"{_lastStarName}\" — sign in to enable open notifications.";
                _sszCreatedWhileLoggedOut = false;
            }
            else if (_isSszMode)
            {
                ZipSuccessHeadline.Text = $"Certified Delivery created!";
                ZipSuccessSubtitle.Text = $"\"{_lastStarName}\" is ready to send. You'll be notified when it's opened.";
            }
            else
            {
                ZipSuccessHeadline.Text = "ZIP created successfully!";
                ZipSuccessSubtitle.Text = "Your encrypted archive is ready.";
            }

            ZipSuccessPath.Text = toCloud ? $"(temp) {output}" : output;

            // Hide form, show success panel
            ZipFormPanel.Visibility       = Visibility.Collapsed;
            ZipSuccessPanel.Visibility    = Visibility.Visible;
            BtnShowInExplorer.IsEnabled   = !toCloud;

            // Cloud upload row
            ZipSuccessCloudRow.Visibility    = Visibility.Collapsed;
            ZipSuccessCloudResult.Visibility = Visibility.Collapsed;
            CopyCloudLinkBtn2.Visibility     = Visibility.Collapsed;

            if (_isSszMode && _lastCreatedSszPath != null)
            {
                if (toCloud)
                {
                    var provider = _saveDest == "GoogleDrive"
                        ? CloudBackup.VaultSyncManager.Providers[0]
                        : CloudBackup.VaultSyncManager.Providers[1];
                    await UploadSszToCloudAsync(provider, _saveDest == "GoogleDrive" ? "Google Drive" : "OneDrive");
                }
                else
                {
                    var gdrive   = CloudBackup.VaultSyncManager.Providers[0];
                    var onedrive = CloudBackup.VaultSyncManager.Providers[1];
                    UploadGDriveBtn2.IsEnabled   = gdrive.IsConnected;
                    UploadOneDriveBtn2.IsEnabled = onedrive.IsConnected;
                    if (gdrive.IsConnected || onedrive.IsConnected)
                        ZipSuccessCloudRow.Visibility = Visibility.Visible;
                }
            }

            // Scroll success panel into view
            ZipSuccessPanel.BringIntoView();

            bool saveChecked = (_pwdMode == "Passphrase")
                ? ChkSavePassphrase.IsChecked == true
                : ChkSavePassword.IsChecked == true;
            if (saveChecked)
                SavedPasswordStore.Save(output, password);
        }
        else ShowZipError($"Error: {errMsg}");
    }

    private string? _lastCreatedSszPath;
    private string? _lastCloudLink;

    private async void UploadGDriveBtn_Click(object sender, RoutedEventArgs e)
        => await UploadSszToCloudAsync(CloudBackup.VaultSyncManager.Providers[0], "Google Drive");

    private async void UploadOneDriveBtn_Click(object sender, RoutedEventArgs e)
        => await UploadSszToCloudAsync(CloudBackup.VaultSyncManager.Providers[1], "OneDrive");

    private async Task UploadSszToCloudAsync(CloudBackup.ICloudProvider provider, string name)
    {
        if (_lastCreatedSszPath == null) return;
        UploadGDriveBtn2.IsEnabled   = false;
        UploadOneDriveBtn2.IsEnabled = false;
        ZipSuccessCloudResult.Visibility = Visibility.Visible;
        ZipSuccessCloudText.Text = $"Uploading to {name}…";
        CopyCloudLinkBtn2.Visibility = Visibility.Collapsed;

        try
        {
            string? link = await provider.UploadSszAsync(_lastCreatedSszPath);
            if (link != null)
            {
                _lastCloudLink = link;
                ZipSuccessCloudText.Text = $"Uploaded to {name}. Share the link:";
                CopyCloudLinkBtn2.Visibility = Visibility.Visible;
                AppLog.Write($"SSZ uploaded to {name}: {link}");

                SavedPasswordStore.UpdateCloudInfo(
                    _lastCreatedSszPath,
                    cloudUrl:      link,
                    cloudFileId:   link,
                    cloudProvider: name);
            }
            else
            {
                ZipSuccessCloudText.Text = $"Upload to {name} failed. Check log for details.";
            }
        }
        catch (Exception ex)
        {
            ZipSuccessCloudText.Text = $"Upload error: {ex.Message}";
            AppLog.Write($"SSZ upload to {name} exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void CopyCloudLinkBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_lastCloudLink != null)
        {
            Clipboard.SetText(_lastCloudLink);
            CopyCloudLinkBtn2.Content = "Copied!";
        }
    }

    private void ZipShowInExplorer_Click(object sender, RoutedEventArgs e)
    {
        string path = ZipSuccessPath.Text;
        if (File.Exists(path))
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        else if (Directory.Exists(path))
            System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
    }

    private void ZipCreateAnother_Click(object sender, RoutedEventArgs e)
    {
        // Reset form and show it again
        ZipSuccessPanel.Visibility = Visibility.Collapsed;
        ZipFormPanel.Visibility    = Visibility.Visible;
        ZipSourceBox.Text          = "No file or folder selected";
        ZipOutputBox.Text          = "";
        ZipPasswordBox.Password    = "";
        ZipConfirmBox.Password     = "";
        ZipErrorText.Visibility    = Visibility.Collapsed;
    }

    // ─── Save destination picker ──────────────────────────────────────────────

    private void RefreshSaveDestButtons()
    {
        var gdrive   = CloudBackup.VaultSyncManager.Providers[0];
        var onedrive = CloudBackup.VaultSyncManager.Providers[1];

        // Update subtitles to reflect connection state
        DestGDriveSub.Text   = gdrive.IsConnected   ? gdrive.ConnectedAccount   ?? "Connected" : "Not connected — tap to connect";
        DestOneDriveSub.Text = onedrive.IsConnected ? onedrive.ConnectedAccount ?? "Connected" : "Not connected — tap to connect";

        // If cloud was selected but we lost connection, reset to local
        if (_saveDest == "GoogleDrive" && !gdrive.IsConnected)   SetSaveDest("Local");
        if (_saveDest == "OneDrive"    && !onedrive.IsConnected) SetSaveDest("Local");

        UpdateSaveDestUI();
    }

    private async void DestTile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border tile || tile.Tag is not string dest) return;

        var gdrive   = CloudBackup.VaultSyncManager.Providers[0];
        var onedrive = CloudBackup.VaultSyncManager.Providers[1];

        if (dest == "GoogleDrive" && !gdrive.IsConnected)
        {
            DestGDriveSub.Text = "Connecting…";
            try
            {
                bool ok = await gdrive.ConnectAsync();
                if (ok) { _ = CloudBackup.VaultSyncManager.PushAsync(); SetSaveDest("GoogleDrive"); }
                else DestGDriveSub.Text = "Cancelled";
            }
            catch { DestGDriveSub.Text = "Error — try again"; }
            finally { RefreshCloudStatus(); }
            return;
        }

        if (dest == "OneDrive" && !onedrive.IsConnected)
        {
            DestOneDriveSub.Text = "Connecting…";
            try
            {
                bool ok = await onedrive.ConnectAsync();
                if (ok) { _ = CloudBackup.VaultSyncManager.PushAsync(); SetSaveDest("OneDrive"); }
                else DestOneDriveSub.Text = "Cancelled";
            }
            catch { DestOneDriveSub.Text = "Error — try again"; }
            finally { RefreshCloudStatus(); }
            return;
        }

        SetSaveDest(dest);
    }

    private void SaveDest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string dest)
            SetSaveDest(dest);
    }

    private void CloudDestConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Settings tab and scroll to cloud backup
        ShowSection("Settings");
    }

    private void SetSaveDest(string dest)
    {
        _saveDest = dest;
        UpdateSaveDestUI();
    }

    // Theme-aware brushes so the destination tiles follow Light/Dark/Titanium
    private static System.Windows.Media.Brush TileActiveBorder   => (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"];
    private static System.Windows.Media.Brush TileInactiveBorder => (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"];
    private static System.Windows.Media.Brush TileActiveBg       => (System.Windows.Media.Brush)Application.Current.Resources["AccentDimBrush"];
    private static System.Windows.Media.Brush TileInactiveBg     => (System.Windows.Media.Brush)Application.Current.Resources["BgCardBrush"];

    private void UpdateSaveDestUI()
    {
        var gdrive   = CloudBackup.VaultSyncManager.Providers[0];
        var onedrive = CloudBackup.VaultSyncManager.Providers[1];
        bool isLocal   = _saveDest == "Local";
        bool isGDrive  = _saveDest == "GoogleDrive";
        bool isOD      = _saveDest == "OneDrive";

        // Style the three tiles
        StyleDestTile(DestTileLocal,    isLocal);
        StyleDestTile(DestTileGDrive,   isGDrive);
        StyleDestTile(DestTileOneDrive, isOD);

        // Dim tiles that require SSZ but we're in ZIP mode
        double cloudOpacity = _isSszMode ? 1.0 : 0.45;
        DestTileGDrive.Opacity   = cloudOpacity;
        DestTileOneDrive.Opacity = cloudOpacity;
        DestTileGDrive.IsEnabled   = _isSszMode;
        DestTileOneDrive.IsEnabled = _isSszMode;

        // Show/hide the local path box
        LocalPathRow.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;

        // Show/hide the cloud info box
        CloudDestRow.Visibility = (!isLocal) ? Visibility.Visible : Visibility.Collapsed;

        if (!isLocal)
        {
            bool connected = isGDrive ? gdrive.IsConnected : onedrive.IsConnected;
            string name    = isGDrive ? "Google Drive" : "OneDrive";
            string account = isGDrive ? (gdrive.ConnectedAccount ?? "") : (onedrive.ConnectedAccount ?? "");

            CloudDestTitle.Text = connected
                ? $"{name} · {account}"
                : $"{name} — Not connected";
            CloudDestSubtitle.Text = connected
                ? $"File will be uploaded to {name} and a shareable link copied to your clipboard."
                : $"Connect {name} in Settings to use this option.";
            CloudDestConnectBtn.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;

            // If not connected, reset to local silently
            if (!connected) { isLocal = true; LocalPathRow.Visibility = Visibility.Visible; }
        }
    }

    private static void StyleDestTile(Border tile, bool active)
    {
        tile.BorderBrush = active ? TileActiveBorder : TileInactiveBorder;
        tile.Background  = active ? TileActiveBg     : TileInactiveBg;
    }

    private Button GetDestButton(string dest) => dest switch
    {
        "GoogleDrive" => BtnDestGDrive,
        "OneDrive"    => BtnDestOneDrive,
        _             => BtnDestLocal,
    };

    private void ShowZipError(string msg)
    {
        ZipErrorText.Text = msg;
        ZipErrorText.Visibility = Visibility.Visible;
    }

    // ─── Unzip ────────────────────────────────────────────────────────────────
    private void SetUnzipSource(string path)
    {
        bool isZip = path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        bool isSsz = path.EndsWith(".ssz", StringComparison.OrdinalIgnoreCase);
        if (!isZip && !isSsz)
        { ShowUnzipError("Please select a .zip or .ssz file."); return; }

        if (isSsz && RequirePro("Opening Starkive Secure Containers (.ssz)")) return;

        // Clear results from any previous extraction
        UnzipSuccessBanner.Visibility = Visibility.Collapsed;
        UnzipErrorText.Visibility     = Visibility.Collapsed;

        UnzipSourceBox.Text = path;
        UnzipOutputBox.Text = Path.Combine(
            Path.GetDirectoryName(path) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.GetFileNameWithoutExtension(path));

        // Auto-fill saved password if found
        var saved = SavedPasswordStore.Find(path);
        if (saved != null)
        {
            UnzipPasswordBox.Password = saved.Password;
            ShowToast($"Password filled from saved entry \"{saved.Hint}\".");
        }
    }

    private void UnzipBrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select file to extract",
            Filter = "Starkive files (*.zip;*.ssz)|*.zip;*.ssz|ZIP Archives (*.zip)|*.zip|Secure Containers (*.ssz)|*.ssz|All Files (*.*)|*.*"
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
        UnzipErrorText.Visibility     = Visibility.Collapsed;
        UnzipSuccessBanner.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(UnzipSourceBox.Text) || UnzipSourceBox.Text == "No file selected")
        { ShowUnzipError("Please select a .zip or .ssz file."); return; }
        if (string.IsNullOrWhiteSpace(UnzipOutputBox.Text))
        { ShowUnzipError("Please specify an output folder."); return; }
        if (UnzipPasswordBox.Password.Length == 0)
        { ShowUnzipError("Please enter the ZIP password."); return; }

        UnzipProgressPanel.Visibility = Visibility.Visible;
        UnzipButton.IsEnabled          = false;

        string zipPath   = UnzipSourceBox.Text;
        string outFolder = UnzipOutputBox.Text;
        string password  = UnzipPasswordBox.Password;
        double progWidth = ((Border)UnzipProgressBar.Parent).ActualWidth;

        var prog = new Progress<(int pct, string status)>(p =>
        {
            UnzipProgressBar.Width = progWidth * p.pct / 100.0;
            UnzipPercentText.Text  = $"{p.pct}%";
            UnzipStatusText.Text   = p.status;
        });

        bool success = false; string? errMsg = null;
        bool isSszSource = zipPath.EndsWith(".ssz", StringComparison.OrdinalIgnoreCase);

        // Peek SSZ header before decryption so we have the token ready for phone-home
        SszHeader? sszHeader = isSszSource ? SszHelper.PeekHeader(zipPath) : null;

        try
        {
            if (isSszSource)
                await Task.Run(() => SszHelper.Open(zipPath, outFolder, password, prog, CancellationToken.None));
            else
                await Task.Run(() => ZipHelper.ExtractEncryptedZip(zipPath, outFolder, password, prog));
            success = true;
        }
        catch (Exception ex) { errMsg = ex.Message; }

        UnzipProgressPanel.Visibility = Visibility.Collapsed;
        UnzipButton.IsEnabled          = true;
        HistoryManager.Add(new HistoryEntry
        {
            Type = OperationType.Unzip, SourcePath = zipPath, OutputPath = outFolder,
            Success = success, ErrorMessage = errMsg ?? string.Empty
        });

        if (success)
        {
            // Reset "Sent by" panel
            UnzipSentByPanel.Visibility = Visibility.Collapsed;
            UnzipSuccessBanner.Visibility = Visibility.Visible;

            // For SSZ files: phone-home and show creator identity
            if (isSszSource && sszHeader != null)
            {
                var sentBy = await ApiService.ReportOpenAsync(
                    sszHeader.FileToken,
                    sszHeader.StarName,
                    Path.GetFileName(zipPath));

                if (!string.IsNullOrWhiteSpace(sentBy))
                {
                    UnzipSentByText.Text        = sentBy;
                    UnzipSentByPanel.Visibility = Visibility.Visible;
                }
            }
        }
        else ShowUnzipError(errMsg?.Contains("password", StringComparison.OrdinalIgnoreCase) == true
            ? "Wrong password or the file is not encrypted."
            : $"Error: {errMsg}");
    }

    private void ShowUnzipError(string msg)
    {
        UnzipErrorText.Text = msg;
        UnzipErrorText.Visibility = Visibility.Visible;
    }

    // ─── History ──────────────────────────────────────────────────────────────
    private void RefreshHistorySection()
    {
        var entries = HistoryManager.Load();
        var vms     = entries.Select(h => new HistoryViewModel(h)).ToList();
        HistoryList.ItemsSource  = vms;
        HistoryEmpty.Visibility  = vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryCount.Text        = vms.Count == 0 ? string.Empty : $"{vms.Count} operation(s)";
    }

    private void HistoryClear_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear all history?", "Starkive",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            HistoryManager.Clear();
            RefreshHistorySection();
        }
    }

    // ─── Vault ────────────────────────────────────────────────────────────────
    private List<VaultEntryViewModel> _vaultVMs = [];

    private void RefreshVaultSection()
    {
        var entries = SavedPasswordStore.GetAll();

        // Badge is driven by the entry's own CloudProvider field — not by whether
        // a drive happens to be connected right now.
        _vaultVMs = entries
            .Select(e =>
            {
                bool   isCloud    = !string.IsNullOrEmpty(e.CloudProvider);
                string cloudLabel = e.CloudProvider ?? "";
                return new VaultEntryViewModel(e, isCloud, cloudLabel);
            })
            .ToList();

        // Header sync badge: only show if at least one connected provider exists
        bool hasCloud     = CloudBackup.VaultSyncManager.ConnectedProviders.Any();
        string syncLabel  = CloudBackup.VaultSyncManager.ConnectedProviders
            .Select(p => p.ProviderName).FirstOrDefault() ?? "";

        VaultList.ItemsSource    = _vaultVMs;
        VaultEmptyState.Visibility = _vaultVMs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Subtitle
        VaultSubtitle.Text = _vaultVMs.Count == 0
            ? "Your saved passwords live here, encrypted on this device."
            : $"{_vaultVMs.Count} saved password{(_vaultVMs.Count == 1 ? "" : "s")}";

        // Nav badge — show count when > 0
        NavVaultBadge.Visibility = _vaultVMs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NavVaultCount.Text       = _vaultVMs.Count.ToString();

        // Cloud sync status badge in header
        if (hasCloud)
        {
            VaultSyncBadge.Visibility = Visibility.Visible;
            VaultSyncBadge.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x22, 0xC5, 0x5E));
            VaultSyncBadge.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x40, 0x16, 0xA3, 0x4A)));
            VaultSyncBadge.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            VaultSyncIcon.Text      = "";  // cloud icon
            VaultSyncIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
            VaultSyncLabel.Text     = $"Synced · {syncLabel}";
            VaultSyncLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
        }
        else
        {
            VaultSyncBadge.Visibility = Visibility.Visible;
            VaultSyncBadge.Background = new SolidColorBrush(Color.FromArgb(0x18, 0x94, 0xA3, 0xB8));
            VaultSyncBadge.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x30, 0x94, 0xA3, 0xB8)));
            VaultSyncBadge.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            VaultSyncIcon.Text       = "";  // local/device icon
            VaultSyncIcon.Foreground  = (SolidColorBrush)FindResource("TextMutedBrush");
            VaultSyncLabel.Text      = "Local only · Connect cloud in Settings";
            VaultSyncLabel.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
        }
    }

    private void VaultReveal_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is string key)
        {
            var vm = _vaultVMs.FirstOrDefault(v => v.Key == key);
            if (vm != null) vm.IsRevealed = !vm.IsRevealed;
        }
    }

    private void VaultCopy_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is string key)
        {
            var vm = _vaultVMs.FirstOrDefault(v => v.Key == key);
            if (vm != null)
            {
                Clipboard.SetText(vm.Password);
                ShowToast("Password copied.");
            }
        }
    }

    private void VaultDelete_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is string key)
        {
            var vm = _vaultVMs.FirstOrDefault(v => v.Key == key);
            string name = vm?.Hint ?? "this entry";
            if (MessageBox.Show($"Delete the saved password for \"{name}\"?",
                "Starkive", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                SavedPasswordStore.DeleteByKey(key);
                RefreshVaultSection();
            }
        }
    }

    private void VaultGoToZip_Click(object sender, RoutedEventArgs e)
        => ShowSection("Zip");

    /// <summary>Opens Explorer with the local encrypted file selected.</summary>
    private void VaultOpenLocal_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is not VaultEntryViewModel vm) return;
        if (string.IsNullOrEmpty(vm.OutputPath)) return;

        if (!System.IO.File.Exists(vm.OutputPath))
        {
            ShowToast("File not found — it may have been moved or deleted.");
            return;
        }

        try
        {
            // Open Explorer with the file highlighted
            System.Diagnostics.Process.Start("explorer.exe",
                $"/select,\"{vm.OutputPath}\"");
        }
        catch (Exception ex)
        {
            AppLog.Write($"VaultOpenLocal error: {ex.Message}");
            ShowToast("Could not open file location.");
        }
    }

    /// <summary>Opens the cloud share link in the default browser.</summary>
    private void VaultOpenCloud_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is not VaultEntryViewModel vm) return;
        if (string.IsNullOrEmpty(vm.CloudUrl)) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = vm.CloudUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLog.Write($"VaultOpenCloud error: {ex.Message}");
            // Fallback: copy to clipboard
            Clipboard.SetText(vm.CloudUrl);
            ShowToast("Could not open browser — link copied to clipboard.");
        }
    }

    // ─── Settings ─────────────────────────────────────────────────────────────
    private void RefreshSettingsSection()
    {
        DefaultFolderBox.Text = _settings.DefaultOutputFolder;

        string appDataPath    = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Starkive");
        AppDataPathText.Text  = appDataPath;

        bool installed         = ContextMenuInstaller.IsInstalled();
        CtxMenuStatusText.Text = installed
            ? "Installed. Right-click any file or folder to zip, or any .zip/.ssz file to decrypt."
            : "Not installed. Click Install to add Starkive to your right-click context menu.";
        CtxMenuButton.Content  = installed ? "Uninstall" : "Install";

        RefreshCloudStatus();
    }

    private void SettingsBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        string folder = BrowseForFolder("Select default output folder");
        if (!string.IsNullOrEmpty(folder)) DefaultFolderBox.Text = folder;
    }

    // ─── Cloud Backup ─────────────────────────────────────────────────────────

    private void RefreshCloudStatus()
    {
        var gdrive   = CloudBackup.VaultSyncManager.Providers[0]; // GoogleDriveProvider
        var onedrive = CloudBackup.VaultSyncManager.Providers[1]; // OneDriveProvider

        bool gConfigured = !string.IsNullOrEmpty(AppConstants.GoogleDriveClientId);
        bool odConfigured = !string.IsNullOrEmpty(AppConstants.OneDriveClientId);

        GDriveStatusText.Text  = !gConfigured  ? "OAuth not configured"
                                : gdrive.IsConnected   ? $"Connected: {gdrive.ConnectedAccount}"
                                : "Not connected";
        OneDriveStatusText.Text = !odConfigured ? "OAuth not configured"
                                : onedrive.IsConnected ? $"Connected: {onedrive.ConnectedAccount}"
                                : "Not connected";

        GDriveButton.Content   = gdrive.IsConnected   ? "Disconnect" : "Connect";
        OneDriveButton.Content = onedrive.IsConnected ? "Disconnect" : "Connect";
        GDriveButton.IsEnabled   = gConfigured;
        OneDriveButton.IsEnabled = odConfigured;

        // Show setup warning only if either credential is missing
        CloudSetupWarning.Visibility = (!gConfigured || !odConfigured)
            ? Visibility.Visible : Visibility.Collapsed;

        // Keep the Zip & Encrypt destination picker in sync
        RefreshSaveDestButtons();
    }

    private async void GDriveButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = CloudBackup.VaultSyncManager.Providers[0];
        GDriveButton.IsEnabled = false;
        GDriveStatusText.Text  = provider.IsConnected ? "Disconnecting…" : "Opening browser…";
        try
        {
            if (provider.IsConnected)
                await provider.DisconnectAsync();
            else
            {
                bool ok = await provider.ConnectAsync();
                if (ok) _ = CloudBackup.VaultSyncManager.PushAsync(); // initial upload
                else GDriveStatusText.Text = "Connection cancelled.";
            }
        }
        catch (Exception ex) { GDriveStatusText.Text = $"Error: {ex.Message}"; }
        finally { RefreshCloudStatus(); GDriveButton.IsEnabled = !string.IsNullOrEmpty(AppConstants.GoogleDriveClientId); }
    }

    private async void OneDriveButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = CloudBackup.VaultSyncManager.Providers[1];
        OneDriveButton.IsEnabled = false;
        OneDriveStatusText.Text  = provider.IsConnected ? "Disconnecting…" : "Opening browser…";
        try
        {
            if (provider.IsConnected)
                await provider.DisconnectAsync();
            else
            {
                bool ok = await provider.ConnectAsync();
                if (ok) _ = CloudBackup.VaultSyncManager.PushAsync(); // initial upload
                else OneDriveStatusText.Text = "Connection cancelled.";
            }
        }
        catch (Exception ex) { OneDriveStatusText.Text = $"Error: {ex.Message}"; }
        finally { RefreshCloudStatus(); OneDriveButton.IsEnabled = !string.IsNullOrEmpty(AppConstants.OneDriveClientId); }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Starkive");
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private void CtxMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ContextMenuInstaller.IsInstalled()) ContextMenuInstaller.Uninstall();
            else ContextMenuInstaller.Install();
            RefreshSettingsSection();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not update context menu:\n{ex.Message}\n\nTry running Starkive as Administrator.",
                "Starkive", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemeChange) return;
        if (ThemeComboBox.SelectedItem is ComboBoxItem item)
        {
            string theme = item.Tag?.ToString() ?? "Dark";
            ApplyTheme(theme);
            _settings.Theme = theme;
            SettingsManager.Save(_settings);
        }
    }

    // ── Theme engine ──────────────────────────────────────────────────────────
    private void ApplyTheme(string theme)
    {
        // Resolve "System" to the OS preference
        if (theme == "System")
            theme = IsOsDarkMode() ? "Dark" : "Light";

        switch (theme)
        {
            case "Light":    ApplyLightTheme();    break;
            case "Titanium": ApplyTitaniumTheme(); break;
            default:         ApplyDarkTheme();     break;  // "Dark" + fallback
        }

        if (ThemeStatusText != null)
            ThemeStatusText.Text = $"{theme} theme is active";

        // Re-apply nav highlight so inactive buttons pick up the new theme colors
        ShowSection(_activeSection);
    }

    // ── Dark  ─────────────────────────────────────────────────────────────────
    // Apple dark mode system palette. Pure blacks, WCAG AA throughout.
    // TextPrimary  #F5F5F7 on #1C1C1E  → 16.1:1  ✓
    // TextSecondary #AEAEB2 on #1C1C1E →  7.3:1  ✓
    // TextMuted    #8E8E93 on #1C1C1E  →  5.1:1  ✓
    // AccentBrush  #0071E3 on #2C2C2E  →  4.5:1  ✓ (buttons: white text 4.7:1)
    private void ApplyDarkTheme()
    {
        SetBrush("BgSurfaceBrush",       "#2C2C2E");  // sidebar: Apple secondarySystemBackground
        SetBrush("BgPrimaryBrush",       "#1C1C1E");  // content: Apple systemBackground
        SetBrush("BgCardBrush",          "#2C2C2E");  // cards: lifted surface
        SetBrush("BgHoverBrush",         "#3A3A3C");  // hover: Apple tertiarySystemBackground
        SetBrush("BgElevatedBrush",      "#3A3A3C");  // elevated controls
        SetBrush("BgInputBrush",         "#1C1C1E");  // inputs: same as content bg
        SetBrush("LogoMarkBgBrush",      "#003B8E");  // logo: deep blue
        SetBrush("NavActiveBgBrush",     "#0071E3");  // active pill: Apple blue
        SetBrush("NavActiveBorderBrush", "Transparent");
        SetBrush("BorderBrush",          "#38383A");  // Apple separator dark
        SetBrush("BorderSubtleBrush",    "#2C2C2E");  // subtle divider
        SetBrush("TextPrimaryBrush",     "#F5F5F7");  // Apple label dark       — 16:1 on #1C1C1E ✓
        SetBrush("TextSecondaryBrush",   "#C7C7CC");  // Apple secondaryLabel  — 10.8:1            ✓
        SetBrush("TextMutedBrush",       "#AEAEB2");  // Apple tertiaryLabel   —  8.2:1            ✓
        SetBrush("TextLabelBrush",       "#98989D");  // section headers       —  6.3:1 on #1C1C1E ✓
        SetBrush("AccentDimBrush",       "#002A6B");  // deep blue tint for selected bg
        SetBrush("AccentGlowBrush",      "#400071E3");
        SetBrush("IconBgBlueBrush",      "#002A6B");
        SetBrush("IconBgGreenBrush",     "#003B1A");
        SetBrush("IconBgPurpleBrush",    "#2A0A50");
        SetBrush("IconBgAmberBrush",     "#3A2400");
        SetBrush("BannerSuccessBgBrush", "#003B1A");
        SetBrush("BannerWarnBgBrush",    "#3A2400");
        SetBrush("BannerInfoBgBrush",    "#002A6B");
        SetBrush("GoProBgBrush",         "#2A0A50");
        SetBrush("GoProBorderBrush",     "#9F67E0");
        SetBrush("ProTextBrush",         "#C084FC");  // bright purple on dark
        SetBrush("ProSubTextBrush",      "#A78BFA");  // softer purple on dark
        SetBrush("ProBadgeBgBrush",      "#3A2070");  // deep purple chip
        SetGradient("HeroGradientBrush", "#2C2C2E", "#1C1C1E");
        SetGradient("ProGradientBrush",  "#A855F7", "#7C3AED");
        SetGradient("AccentGradientBrush","#0071E3","#0077ED");
    }

    // ── Light  ────────────────────────────────────────────────────────────────
    // Apple light mode system palette. Pure white content, F5F5F7 sidebar.
    // TextPrimary  #1D1D1F on #FFFFFF  → 19.1:1  ✓
    // TextSecondary #3A3A3C on #FFFFFF →  9.7:1  ✓
    // TextMuted    #6E6E73 on #FFFFFF  →  4.6:1  ✓
    // AccentBrush  #0071E3 on #FFFFFF  →  4.5:1  ✓ (buttons: white text 4.7:1)
    private void ApplyLightTheme()
    {
        SetBrush("BgSurfaceBrush",       "#F5F5F7");  // sidebar: Apple secondarySystemBackground
        SetBrush("BgPrimaryBrush",       "#FFFFFF");  // content: pure white
        SetBrush("BgCardBrush",          "#FFFFFF");  // cards: white
        SetBrush("BgHoverBrush",         "#F0F0F2");  // hover: subtle off-white
        SetBrush("BgElevatedBrush",      "#F5F5F7");  // elevated controls
        SetBrush("BgInputBrush",         "#FFFFFF");  // inputs: white
        SetBrush("LogoMarkBgBrush",      "#EBF3FF");  // logo: light blue tint
        SetBrush("NavActiveBgBrush",     "#EBF3FF");  // active nav: blue tint
        SetBrush("NavActiveBorderBrush", "#0071E3");  // active nav: blue accent line
        SetBrush("BorderBrush",          "#D2D2D7");  // Apple separator light
        SetBrush("BorderSubtleBrush",    "#E8E8ED");  // Apple subtle separator
        SetBrush("TextPrimaryBrush",     "#1D1D1F");  // Apple label light
        SetBrush("TextSecondaryBrush",   "#3A3A3C");  // Apple secondaryLabel light
        SetBrush("TextMutedBrush",       "#6E6E73");  // Apple tertiaryLabel light
        SetBrush("TextLabelBrush",       "#86868B");  // Apple quaternaryLabel light
        SetBrush("AccentDimBrush",       "#EBF3FF");  // blue tint background
        SetBrush("AccentGlowBrush",      "#200071E3");
        SetBrush("IconBgBlueBrush",      "#EBF3FF");
        SetBrush("IconBgGreenBrush",     "#E8FFF0");
        SetBrush("IconBgPurpleBrush",    "#F3EFFE");
        SetBrush("IconBgAmberBrush",     "#FFF8E7");
        SetBrush("BannerSuccessBgBrush", "#E8FFF0");
        SetBrush("BannerWarnBgBrush",    "#FFF8E7");
        SetBrush("BannerInfoBgBrush",    "#EBF3FF");
        SetBrush("GoProBgBrush",         "#F3EFFE");
        SetBrush("GoProBorderBrush",     "#9F67E0");
        SetBrush("ProTextBrush",         "#6D28D9");  // deep purple on light — 6.6:1 on #F3EFFE ✓
        SetBrush("ProSubTextBrush",      "#7C5DA8");  // muted purple on light
        SetBrush("ProBadgeBgBrush",      "#E5D8FA");  // pale purple chip
        SetGradient("HeroGradientBrush", "#F5F5F7", "#FFFFFF");
        SetGradient("ProGradientBrush",  "#A855F7", "#7C3AED");
        SetGradient("AccentGradientBrush","#0071E3","#0077ED");
    }

    // ── Titanium (Antares) ────────────────────────────────────────────────────
    // Warm charcoal. Every layer stepped 12-15 luminance points for clear depth.
    // TextPrimary  #F2EDE4 on #1E1B17  → 14.8:1  ✓
    // TextSecondary #B8B0A6 on #1E1B17 →  7.1:1  ✓
    // TextMuted    #8A8278 on #1E1B17  →  4.9:1  ✓
    // AccentBrush  #0071E3 on #2E2A25  →  4.5:1  ✓ (buttons: white text 4.7:1)
    private void ApplyTitaniumTheme()
    {
        SetBrush("BgSurfaceBrush",       "#2E2A25");  // sidebar: warm charcoal lifted
        SetBrush("BgPrimaryBrush",       "#1E1B17");  // content: warm near-black
        SetBrush("BgCardBrush",          "#2E2A25");  // cards: lifted warm layer
        SetBrush("BgHoverBrush",         "#3E3A34");  // hover: warm mid
        SetBrush("BgElevatedBrush",      "#3E3A34");  // elevated controls
        SetBrush("BgInputBrush",         "#1E1B17");  // inputs: content bg
        SetBrush("LogoMarkBgBrush",      "#002060");  // logo: cool contrast on warm
        SetBrush("NavActiveBgBrush",     "#0071E3");  // active pill: blue pops on warm
        SetBrush("NavActiveBorderBrush", "Transparent");
        SetBrush("BorderBrush",          "#4E4A44");  // warm medium separator
        SetBrush("BorderSubtleBrush",    "#3E3A34");  // warm subtle separator
        SetBrush("TextPrimaryBrush",     "#F2EDE4");  // warm near-white — 14.8:1 ✓
        SetBrush("TextSecondaryBrush",   "#CEC8C0");  // warm medium     —  9.8:1 ✓
        SetBrush("TextMutedBrush",       "#B8B0A6");  // warm muted      —  7.1:1 ✓
        SetBrush("TextLabelBrush",       "#9A948C");  // section headers —  5.1:1 ✓
        SetBrush("AccentDimBrush",       "#002060");  // deep blue tint
        SetBrush("AccentGlowBrush",      "#400071E3");
        SetBrush("IconBgBlueBrush",      "#1A2840");
        SetBrush("IconBgGreenBrush",     "#0E2218");
        SetBrush("IconBgPurpleBrush",    "#2A1040");
        SetBrush("IconBgAmberBrush",     "#3A2810");
        SetBrush("BannerSuccessBgBrush", "#0E2218");
        SetBrush("BannerWarnBgBrush",    "#3A2810");
        SetBrush("BannerInfoBgBrush",    "#1A2840");
        SetBrush("GoProBgBrush",         "#2A1040");
        SetBrush("GoProBorderBrush",     "#9F67E0");
        SetBrush("ProTextBrush",         "#C9A8F0");  // warm-leaning purple on charcoal
        SetBrush("ProSubTextBrush",      "#AC92D8");  // softer warm purple
        SetBrush("ProBadgeBgBrush",      "#3A2458");  // deep purple chip
        SetGradient("HeroGradientBrush", "#2E2A25", "#1E1B17");
        SetGradient("ProGradientBrush",  "#A855F7", "#7C3AED");
        SetGradient("AccentGradientBrush","#0071E3","#0077ED");
    }

    private void SetBrush(string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        if (Application.Current.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
            brush.Color = color;
        else
            Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    /// <summary>Replaces a LinearGradientBrush resource. Hero = top→bottom (0,0→0,1); others = diagonal (0,0→1,1).</summary>
    private void SetGradient(string key, string hexStart, string hexEnd)
    {
        var c0 = (Color)ColorConverter.ConvertFromString(hexStart);
        var c1 = (Color)ColorConverter.ConvertFromString(hexEnd);
        bool vertical = key == "HeroGradientBrush";
        var grad = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint   = vertical ? new System.Windows.Point(0, 1) : new System.Windows.Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(c0, 0.0),
                new GradientStop(c1, 1.0),
            }
        };
        Application.Current.Resources[key] = grad;
    }

    private static bool IsOsDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser
                .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return (key?.GetValue("AppsUseLightTheme") is int v && v == 0);
        }
        catch { return true; }
    }

    // ── Pro status UI ─────────────────────────────────────────────────────────
    private void RefreshProStatus()
    {
        bool isPro = AuthManager.IsProUser;

        // Sidebar card — colors come from theme-aware Pro brushes in XAML;
        // only the wording changes between free and Pro.
        GoProLabel.Text = isPro ? "Starkive Pro" : "Go Pro";
        GoProPrice.Text = isPro ? "Active ✓"     : "$2.99/mo";

        // Settings page — swap upgrade card ↔ active badge
        ProUpgradeCard.Visibility = isPro ? Visibility.Collapsed : Visibility.Visible;
        ProActiveCard.Visibility  = isPro ? Visibility.Visible   : Visibility.Collapsed;

        // Home page — hide upsell strip, show Pro-active ribbon; hide PRO badge on card
        HomeProUpsellStrip.Visibility  = isPro ? Visibility.Collapsed : Visibility.Visible;
        HomeProActiveStrip.Visibility  = isPro ? Visibility.Visible   : Visibility.Collapsed;
        HomeCardProBadge.Visibility    = isPro ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _settings.DefaultOutputFolder = DefaultFolderBox.Text;
        SettingsManager.Save(_settings);
        SettingsSavedBanner.Visibility = Visibility.Visible;
    }

    // ─── Toast ────────────────────────────────────────────────────────────────
    private void ShowToast(string message)
    {
        ToastText.Text          = message;
        ToastBorder.Visibility  = Visibility.Visible;
        ToastBorder.Opacity     = 1;
        ToastTranslate.Y        = 20;

        var slideIn = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);
        ToastBorder.BeginAnimation(OpacityProperty, fadeIn);

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
        _toastTimer.Tick += (s, _) =>
        {
            _toastTimer?.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (_, _) => ToastBorder.Visibility = Visibility.Collapsed;
            ToastBorder.BeginAnimation(OpacityProperty, fadeOut);
        };
        _toastTimer.Start();
    }

    // ─── Folder picker helper ─────────────────────────────────────────────────
    private static string BrowseForFolder(string description)
    {
        var dlg = new OpenFileDialog
        {
            Title = description, CheckFileExists = false, CheckPathExists = true,
            FileName = "Select Folder", Filter = "Folders|*.none",
            ValidateNames = false
        };
        if (dlg.ShowDialog() == true)
            return Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
        return string.Empty;
    }
}
