using System.ComponentModel;
using System.Windows;

namespace Starkive;

internal sealed class VaultEntryViewModel : INotifyPropertyChanged
{
    private readonly SavedEntry _entry;
    private readonly bool       _cloudSynced;
    private readonly string     _cloudProviderName;
    private bool                _isRevealed;

    internal VaultEntryViewModel(SavedEntry entry, bool cloudSynced, string cloudProviderName)
    {
        _entry             = entry;
        _cloudSynced       = cloudSynced;
        _cloudProviderName = cloudProviderName;
    }

    // ── Identity (used as Tag in buttons so code-behind can look up the VM) ───
    public string Key      => _entry.Key;
    public string Hint     => _entry.Hint;
    public string Password => _entry.Password;

    // ── Display ───────────────────────────────────────────────────────────────
    public string FormattedDate
    {
        get
        {
            var age = DateTime.UtcNow - _entry.SavedAt;
            return age.TotalDays < 1   ? "Today" :
                   age.TotalDays < 2   ? "Yesterday" :
                   age.TotalDays < 7   ? $"{(int)age.TotalDays} days ago" :
                   age.TotalDays < 30  ? $"{(int)(age.TotalDays / 7)} week{((int)(age.TotalDays / 7) == 1 ? "" : "s")} ago" :
                   _entry.SavedAt.ToLocalTime().ToString("MMM d, yyyy");
        }
    }

    // ── Cloud / local badges ──────────────────────────────────────────────────
    public Visibility CloudBadgeVisibility => _cloudSynced ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LocalBadgeVisibility => _cloudSynced ? Visibility.Collapsed : Visibility.Visible;
    public string     CloudLabel           => _cloudProviderName;

    // ── Password reveal ───────────────────────────────────────────────────────
    public bool IsRevealed
    {
        get => _isRevealed;
        set
        {
            _isRevealed = value;
            OnPropertyChanged(nameof(IsRevealed));
            OnPropertyChanged(nameof(PasswordDisplay));
            OnPropertyChanged(nameof(RevealLabel));
        }
    }

    public string PasswordDisplay => _isRevealed
        ? _entry.Password
        : new string('•', Math.Clamp(_entry.Password.Length, 8, 20));

    public string RevealLabel => _isRevealed ? "Hide" : "Show";

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
