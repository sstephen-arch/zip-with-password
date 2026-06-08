using System.Windows;
using System.Windows.Input;

namespace Starkive;

public partial class AuthDialog : Window
{
    private bool _isSignUp = false;

    public AuthDialog()
    {
        InitializeComponent();
        EmailBox.Focus();
    }

    // ── Mode toggle ───────────────────────────────────────────────────────────

    private void ToggleMode_Click(object sender, MouseButtonEventArgs e) => SetMode(!_isSignUp);

    private void SetMode(bool signUp)
    {
        _isSignUp = signUp;

        SubheadingText.Text  = signUp ? "Create your account" : "Sign in to your account";
        PrimaryBtn.Content   = signUp ? "Create Account"      : "Sign In";
        ToggleLabel.Text     = signUp ? "Already have an account?" : "Don't have an account?";
        ToggleLink.Text      = signUp ? " Sign in"            : " Create one";
        ConfirmRow.Visibility = signUp ? Visibility.Visible   : Visibility.Collapsed;

        ClearError();
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    private async void PrimaryBtn_Click(object sender, RoutedEventArgs e)
    {
        ClearError();

        string email    = EmailBox.Text.Trim();
        string password = PasswordBox.Password;

        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        { ShowError("Enter a valid email address."); return; }

        if (password.Length < 8)
        { ShowError("Password must be at least 8 characters."); return; }

        if (_isSignUp && ConfirmBox.Password != password)
        { ShowError("Passwords do not match."); return; }

        SetBusy(true);

        LoginResult result = _isSignUp
            ? await AuthManager.SignUpAsync(email, password)
            : await AuthManager.SignInAsync(email, password);

        SetBusy(false);

        if (result.Success)
            DialogResult = true;
        else
            ShowError(result.Error ?? "Something went wrong. Please try again.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ShowError(string msg)
    {
        ErrorText.Text         = msg;
        ErrorBorder.Visibility = Visibility.Visible;
    }

    private void ClearError() => ErrorBorder.Visibility = Visibility.Collapsed;

    private void SetBusy(bool busy)
    {
        PrimaryBtn.IsEnabled  = !busy;
        EmailBox.IsEnabled    = !busy;
        PasswordBox.IsEnabled = !busy;
        ConfirmBox.IsEnabled  = !busy;
        PrimaryBtn.Content    = busy ? "Please wait…"
                                     : (_isSignUp ? "Create Account" : "Sign In");
    }
}
