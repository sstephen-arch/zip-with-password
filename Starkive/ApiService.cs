using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Starkive;

/// <summary>
/// Thin wrapper around HttpClient for all Supabase REST calls.
/// One instance for the lifetime of the process — never create a second HttpClient.
/// </summary>
internal static class ApiService
{
    private static readonly HttpClient _http = BuildClient();

    private static HttpClient BuildClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Add("apikey", AppConstants.SupabaseAnonKey);
        client.DefaultRequestHeaders.Add("User-Agent", $"Starkive/{AppConstants.AppVersion}");
        return client;
    }

    // Called by AuthManager after a successful login or token refresh.
    internal static void SetAuthToken(string accessToken)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    internal static void ClearAuthToken()
    {
        _http.DefaultRequestHeaders.Authorization = null;
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    internal static Task<AuthResponse?> SignUpAsync(string email, string password) =>
        PostAuthAsync("/auth/v1/signup", new { email, password });

    internal static Task<AuthResponse?> SignInAsync(string email, string password) =>
        PostAuthAsync("/auth/v1/token?grant_type=password", new { email, password });

    internal static Task<AuthResponse?> RefreshAsync(string refreshToken) =>
        PostAuthAsync("/auth/v1/token?grant_type=refresh_token",
                      new { refresh_token = refreshToken });

    // ── Email OTP (passwordless) ──────────────────────────────────────────────

    /// <summary>
    /// Sends a 6-digit OTP to the given email. Creates the account if it doesn't exist.
    /// Returns true on success.
    /// </summary>
    internal static async Task<bool> SendOtpAsync(string email)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                AppConstants.SupabaseUrl + "/auth/v1/otp",
                new { email, create_user = true });
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>
    /// Verifies the OTP code the user typed. Returns a full AuthResponse on success, null on failure.
    /// </summary>
    internal static async Task<AuthResponse?> VerifyOtpAsync(string email, string token)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                AppConstants.SupabaseUrl + "/auth/v1/verify",
                new { type = "email", email, token });
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AuthResponse>();
        }
        catch { return null; }
    }

    private static async Task<AuthResponse?> PostAuthAsync(string path, object body)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(AppConstants.SupabaseUrl + path, body);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AuthResponse>();
        }
        catch { return null; }
    }

    // ── SSZ file registration ─────────────────────────────────────────────────

    internal static async Task RegisterSszFileAsync(SszFileRecord record)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                AppConstants.SupabaseUrl + "/rest/v1/ssz_files",
                record);
            if (!resp.IsSuccessStatusCode)
            {
                string body = await resp.Content.ReadAsStringAsync();
                AppLog.Write($"RegisterSszFile failed: HTTP {(int)resp.StatusCode} — {body}");
            }
            else
            {
                AppLog.Write($"RegisterSszFile OK for token {record.FileToken}");
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"RegisterSszFile exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── Open notification (phone-home) ────────────────────────────────────────

    /// <summary>
    /// Calls report-open and returns the creator's email if the server provides it,
    /// or null on failure. Retries once on network error.
    /// </summary>
    internal static async Task<string?> ReportOpenAsync(string fileToken, string starName = "", string fileName = "")
    {
        const string url = AppConstants.SupabaseUrl + "/functions/v1/report-open";
        var payload = new { file_token = fileToken, star_name = starName, file_name = fileName };

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.Add("apikey", AppConstants.SupabaseAnonKey);
                var resp = await client.PostAsJsonAsync(url, payload);
                AppLog.Write($"ReportOpen attempt {attempt}: HTTP {(int)resp.StatusCode}");
                if (resp.IsSuccessStatusCode)
                {
                    // Edge function returns {"ok":true,"sent_by":"email"} — extract sent_by if present
                    try
                    {
                        var json = await resp.Content.ReadFromJsonAsync<ReportOpenResponse>();
                        return json?.SentBy;
                    }
                    catch { return null; }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write($"ReportOpen attempt {attempt} error: {ex.GetType().Name}: {ex.Message}");
                if (attempt == 1) await Task.Delay(2000);
            }
        }
        return null;
    }

    private sealed class ReportOpenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("sent_by")]
        public string? SentBy { get; init; }
    }

    // ── Pro status ────────────────────────────────────────────────────────────

    /// <summary>
    /// Calls the get_pro_status() RPC. Returns true if the user is Pro (by payment or domain).
    /// Auto-upgrades the user_profiles row server-side if domain matches.
    /// </summary>
    internal static async Task<bool> FetchProStatusAsync()
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                AppConstants.SupabaseUrl + "/rest/v1/rpc/get_pro_status", new { });
            if (!resp.IsSuccessStatusCode) return false;
            var text = await resp.Content.ReadAsStringAsync();
            return text.Trim() == "true";
        }
        catch { return false; }
    }

    // ── Version check ─────────────────────────────────────────────────────────

    // ── GitHub release check ──────────────────────────────────────────────────
    // Hits the GitHub Releases API — no edge function required.
    // Returns the latest semver string (e.g. "1.4.0"), or null on any failure.

    internal static async Task<(string? Version, string? HtmlUrl)> GetLatestReleaseAsync()
    {
        const string url = "https://api.github.com/repos/sstephen-arch/zip-with-password/releases/latest";
        try
        {
            // GitHub API requires a User-Agent header (already set on _http via BuildClient)
            var resp = await _http.GetFromJsonAsync<GitHubReleaseResponse>(url);
            if (resp is null) return (null, null);

            // tag_name is "v1.3.0" — strip the leading 'v' for version comparison
            string version = resp.TagName?.TrimStart('v') ?? "";
            return (version, resp.HtmlUrl);
        }
        catch { return (null, null); }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

internal sealed class AuthResponse
{
    [JsonPropertyName("access_token")]  public string AccessToken  { get; init; } = "";
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; init; } = "";
    [JsonPropertyName("expires_in")]    public int    ExpiresIn    { get; init; }
    [JsonPropertyName("user")]          public AuthUser? User      { get; init; }
}

internal sealed class AuthUser
{
    [JsonPropertyName("id")]    public string Id    { get; init; } = "";
    [JsonPropertyName("email")] public string Email { get; init; } = "";
}

internal sealed class SszFileRecord
{
    [JsonPropertyName("owner_id")]          public string OwnerId         { get; init; } = "";
    [JsonPropertyName("file_token")]        public string FileToken       { get; init; } = "";
    [JsonPropertyName("original_filename")] public string OriginalFilename { get; init; } = "";
    [JsonPropertyName("file_size_bytes")]   public long?  FileSizeBytes   { get; init; }
    [JsonPropertyName("sha256_hash")]       public string Sha256Hash      { get; init; } = "";
    [JsonPropertyName("recipient_hint")]    public string? RecipientHint  { get; init; }
    [JsonPropertyName("star_name")]         public string? StarName       { get; init; }
}

// GitHub Releases API minimal shape
internal sealed class GitHubReleaseResponse
{
    [JsonPropertyName("tag_name")] public string? TagName  { get; init; }
    [JsonPropertyName("html_url")] public string? HtmlUrl  { get; init; }
    [JsonPropertyName("name")]     public string? Name     { get; init; }
}
