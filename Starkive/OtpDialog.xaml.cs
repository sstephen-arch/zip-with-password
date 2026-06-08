using System.Windows;
using System.Windows.Input;

namespace Starkive;

public partial class OtpDialog : Window
{
    private string _email = "";
    private bool _onCodeStep = false;

    public OtpDialog()
    {
        InitializeComponent();
        EmailBox.Focus();
    }

    // ── Step 1: send OTP ──────────────────────────────────────────────────────

    private async void PrimaryBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_onCodeStep)
            await VerifyCodeAsync();
        else
            await SendOtpAsync();
    }

    private async Task SendOtpAsync()
    {
        ClearError();
        _email = EmailBox.Text.Trim();
        if (string.IsNullOrEmpty(_email) || !_email.Contains('@'))
        { ShowError("Enter a valid email address."); return; }

        SetBusy(true);
        bool ok = await ApiService.SendOtpAsync(_email);
        SetBusy(false);

        if (!ok)
        { ShowError("Could not send code. Check your email and try again."); return; }

        ShowCodeStep();
    }

    // ── Step 2: verify code ───────────────────────────────────────────────────

    private async Task VerifyCodeAsync()
    {
        ClearError();
        string code = CodeBox.Text.Trim();
        if (code.Length != 6 || !code.All(char.IsDigit))
        { ShowError("Enter the 6-digit code from your email."); return; }

        SetBusy(true);
        var auth = await ApiService.VerifyOtpAsync(_email, code);
        SetBusy(false);

        if (auth == null)
        { ShowError("Invalid or expired code. Request a new one."); return; }

        await AuthManager.ApplyAuthResponseAsync(auth);
        DialogResult = true;
    }

    // ── Resend / Back ─────────────────────────────────────────────────────────

    private async void ResendLink_Click(object sender, MouseButtonEventArgs e)
    {
        ClearError();
        SetBusy(true);
        bool ok = await ApiService.SendOtpAsync(_email);
        SetBusy(false);
        if (!ok) ShowError("Could not resend code. Please try again.");
        else     CodeBox.Clear();
    }

    private void BackLink_Click(object sender, MouseButtonEventArgs e) => ShowEmailStep();

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void EmailBox_KeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Return) PrimaryBtn_Click(sender, new RoutedEventArgs()); }

    private void CodeBox_KeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Return) PrimaryBtn_Click(sender, new RoutedEventArgs()); }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void ShowCodeStep()
    {
        _onCodeStep = true;
        SubheadingText.Text   = $"We sent a 6-digit code to {_email}.";
        CodeHintText.Text     = "Enter the code below to confirm your address. It expires in 10 minutes.";
        EmailPanel.Visibility = Visibility.Collapsed;
        CodePanel.Visibility  = Visibility.Visible;
        BackLink.Visibility   = Visibility.Visible;
        PrimaryBtn.Content    = "Verify";
        ClearError();
        CodeBox.Focus();
    }

    private void ShowEmailStep()
    {
        _onCodeStep = false;
        SubheadingText.Text   = "Enter your email to sign in or create an account.";
        EmailPanel.Visibility = Visibility.Visible;
        CodePanel.Visibility  = Visibility.Collapsed;
        BackLink.Visibility   = Visibility.Collapsed;
        PrimaryBtn.Content    = "Continue";
        ClearError();
        EmailBox.Focus();
    }

    private void ShowError(string msg)
    {
        ErrorText.Text         = msg;
        ErrorBorder.Visibility = Visibility.Visible;
    }

    private void ClearError() => ErrorBorder.Visibility = Visibility.Collapsed;

    private void SetBusy(bool busy)
    {
        PrimaryBtn.IsEnabled   = !busy;
        EmailBox.IsEnabled     = !busy;
        CodeBox.IsEnabled      = !busy;
        ResendLink.IsEnabled   = !busy;
        PrimaryBtn.Content     = busy ? "Please wait…"
                                      : (_onCodeStep ? "Verify" : "Continue");
    }
}
