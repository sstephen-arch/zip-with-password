using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starkive;

/// <summary>
/// Manages Supabase auth tokens on disk, encrypted with Windows DPAPI.
/// Tokens are tied to the current Windows user account — no key management needed.
/// </summary>
internal static class AuthManager
{
    private static readonly string AuthFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Starkive", "auth.json");

    private static AuthSession? _session;

    public static bool IsLoggedIn  => _session != null;
    public static bool IsProUser   => _session?.IsPro ?? false;
    public static string? UserId   => _session?.UserId;
    public static string? UserEmail => _session?.Email;

    // ── Startup initialisation ────────────────────────────────────────────────

    /// <summary>
    /// Call from App.OnStartup (fire-and-forget: _ = AuthManager.InitializeAsync()).
    /// Loads stored session, refreshes token if near expiry, updates ApiService.
    /// </summary>
    public static async Task InitializeAsync()
    {
        try
        {
            _session = Load();
            if (_session == null) return;

            // Refresh if token expires within the next 5 minutes.
            if (_session.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
            {
                var refreshed = await ApiService.RefreshAsync(_session.RefreshToken);
                if (refreshed == null)
                {
                    // Refresh failed — session is invalid, clear it.
                    Logout();
                    return;
                }
                _session = _session with
                {
                    AccessToken  = refreshed.AccessToken,
                    RefreshToken = refreshed.RefreshToken,
                    ExpiresAt    = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn),
                };
                Save(_session);
            }

            ApiService.SetAuthToken(_session.AccessToken);
            bool isPro = await ApiService.FetchProStatusAsync();
            if (isPro != _session.IsPro) SetProStatus(isPro);
        }
        catch { /* never surface auth errors at startup */ }
    }

    // ── Login / Signup ────────────────────────────────────────────────────────

    public static async Task<LoginResult> SignInAsync(string email, string password)
    {
        var resp = await ApiService.SignInAsync(email, password);
        if (resp == null) return new LoginResult(false, "Sign-in failed. Check your email and password.");
        return FinalizeAuth(resp);
    }

    public static async Task<LoginResult> SignUpAsync(string email, string password)
    {
        var resp = await ApiService.SignUpAsync(email, password);
        if (resp == null) return new LoginResult(false, "Sign-up failed. The email may already be registered.");
        return FinalizeAuth(resp);
    }

    /// <summary>
    /// Called by OtpDialog after a successful VerifyOtp response.
    /// </summary>
    public static async Task ApplyAuthResponseAsync(AuthResponse resp)
    {
        FinalizeAuth(resp);
        bool isPro = await ApiService.FetchProStatusAsync();
        SetProStatus(isPro);
    }

    private static LoginResult FinalizeAuth(AuthResponse resp)
    {
        _session = new AuthSession(
            AccessToken:  resp.AccessToken,
            RefreshToken: resp.RefreshToken,
            ExpiresAt:    DateTime.UtcNow.AddSeconds(resp.ExpiresIn),
            UserId:       resp.User?.Id ?? "",
            Email:        resp.User?.Email ?? "",
            IsPro:        false   // fetched from user_profiles on next request
        );
        Save(_session);
        ApiService.SetAuthToken(_session.AccessToken);
        return new LoginResult(true, null);
    }

    public static void Logout()
    {
        _session = null;
        ApiService.ClearAuthToken();
        try { if (File.Exists(AuthFilePath)) File.Delete(AuthFilePath); } catch { }
    }

    // Sets IsPro after fetching user_profiles from Supabase (called post-login).
    public static void SetProStatus(bool isPro)
    {
        if (_session == null) return;
        _session = _session with { IsPro = isPro };
        Save(_session);
    }

    // ── Persistence (DPAPI) ───────────────────────────────────────────────────

    private static void Save(AuthSession session)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AuthFilePath)!);
            var json      = JsonSerializer.Serialize(session);
            var plaintext = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(AuthFilePath, encrypted);
        }
        catch { }
    }

    private static AuthSession? Load()
    {
        try
        {
            if (!File.Exists(AuthFilePath)) return null;
            var encrypted = File.ReadAllBytes(AuthFilePath);
            var plaintext = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<AuthSession>(Encoding.UTF8.GetString(plaintext));
        }
        catch { return null; }
    }
}

// ── Records ───────────────────────────────────────────────────────────────────

internal sealed record AuthSession(
    [property: JsonPropertyName("access_token")]  string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_at")]    DateTime ExpiresAt,
    [property: JsonPropertyName("user_id")]       string UserId,
    [property: JsonPropertyName("email")]         string Email,
    [property: JsonPropertyName("is_pro")]        bool IsPro
);

internal sealed record LoginResult(bool Success, string? Error);
