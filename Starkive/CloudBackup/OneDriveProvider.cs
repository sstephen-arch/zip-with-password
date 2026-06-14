using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starkive.CloudBackup;

/// <summary>
/// OneDrive backup provider via Microsoft Graph API.
/// Stores "StarkiveVault.enc" in the user's OneDrive Special/approot folder.
///
/// Setup: Register an app at https://portal.azure.com → App registrations
///   • Platform: Mobile and desktop applications
///   • Redirect URI: http://localhost:7839/oauth/callback
///   • API permissions: Files.ReadWrite.AppFolder, User.Read
///   • Paste the Application (client) ID below.
/// </summary>
internal sealed class OneDriveProvider : ICloudProvider
{
    // ── Developer credentials — fill these in ────────────────────────────────
    private const string ClientId = AppConstants.OneDriveClientId;
    // ─────────────────────────────────────────────────────────────────────────

    private const string Authority     = "https://login.microsoftonline.com/consumers/oauth2/v2.0";
    private const string GraphBase     = "https://graph.microsoft.com/v1.0";
    private const string Scope         = "Files.ReadWrite.AppFolder Files.ReadWrite User.Read offline_access";
    private const string BackupFileName = "StarkiveVault.enc";

    private static readonly string _tokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Starkive", "cloud", "onedrive_token.json");

    private static readonly HttpClient _sharedHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

    private OneDriveToken? _token;

    public string  ProviderName     => "OneDrive";
    public bool    IsConnected      => _token != null && !string.IsNullOrEmpty(_token.AccessToken);
    public string? ConnectedAccount => _token?.Email;

    public OneDriveProvider() => _token = LoadToken();

    // ── Connect ───────────────────────────────────────────────────────────────

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        string verifier  = OAuthHelper.GenerateCodeVerifier();
        string challenge = OAuthHelper.GenerateCodeChallenge(verifier);
        string state     = OAuthHelper.GenerateState();

        string authUrl = $"{Authority}/authorize"
            + $"?client_id={Uri.EscapeDataString(ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(OAuthHelper.RedirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(Scope)}"
            + $"&code_challenge={challenge}"
            + "&code_challenge_method=S256"
            + $"&state={state}";

        string query = await OAuthHelper.WaitForCallbackAsync(authUrl, ct);

        string? returnedState = OAuthHelper.ExtractParam(query, "state");
        string? code          = OAuthHelper.ExtractParam(query, "code");
        if (returnedState != state || string.IsNullOrEmpty(code)) return false;

        _token = await ExchangeCodeAsync(code, verifier, ct);
        if (_token == null) return false;

        _token = _token with { Email = await FetchEmailAsync(_token.AccessToken, ct) };
        SaveToken(_token);
        return true;
    }

    public async Task DisconnectAsync()
    {
        _token = null;
        if (File.Exists(_tokenPath)) File.Delete(_tokenPath);
        await Task.CompletedTask;
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    public async Task UploadVaultAsync(byte[] encryptedBytes, CancellationToken ct = default)
    {
        await EnsureFreshTokenAsync(ct);
        var http = BuildClient();

        // PUT to special/approot:/{filename}:/content
        var content = new ByteArrayContent(encryptedBytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

        var resp = await http.PutAsync(
            $"{GraphBase}/me/drive/special/approot:/{BackupFileName}:/content", content, ct);

        if (!resp.IsSuccessStatusCode)
        {
            string err = await resp.Content.ReadAsStringAsync(ct);
            AppLog.Write($"OneDrive upload failed: {(int)resp.StatusCode} — {err}");
            throw new IOException($"OneDrive upload failed: {(int)resp.StatusCode}");
        }
    }

    // ── Upload SSZ file (shareable) ───────────────────────────────────────────

    public async Task<string?> UploadSszAsync(string filePath, CancellationToken ct = default)
    {
        await EnsureFreshTokenAsync(ct);
        var http = BuildClient();

        string fileName  = Path.GetFileName(filePath);
        byte[] fileBytes = await File.ReadAllBytesAsync(filePath, ct);

        // PUT to /me/drive/root:/Starkive/{filename}:/content
        var content = new ByteArrayContent(fileBytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

        var putResp = await http.PutAsync(
            $"{GraphBase}/me/drive/root:/Starkive/{Uri.EscapeDataString(fileName)}:/content",
            content, ct);

        if (!putResp.IsSuccessStatusCode)
        {
            string err = await putResp.Content.ReadAsStringAsync(ct);
            AppLog.Write($"OneDrive SSZ upload failed: {(int)putResp.StatusCode} — {err}");
            return null;
        }

        var item = await putResp.Content.ReadFromJsonAsync<GraphItem>(cancellationToken: ct);
        if (item?.Id == null) return null;

        // Create a shareable link (anyone with link can view)
        var linkResp = await http.PostAsJsonAsync(
            $"{GraphBase}/me/drive/items/{item.Id}/createLink",
            new { type = "view", scope = "anonymous" }, ct);

        if (!linkResp.IsSuccessStatusCode)
        {
            AppLog.Write($"OneDrive createLink failed: {(int)linkResp.StatusCode}");
            return item.WebUrl; // fall back to direct URL
        }

        var linkData = await linkResp.Content.ReadFromJsonAsync<GraphShareLink>(cancellationToken: ct);
        return linkData?.Link?.WebUrl ?? item.WebUrl;
    }

    // ── Download ──────────────────────────────────────────────────────────────

    public async Task<byte[]?> DownloadVaultAsync(CancellationToken ct = default)
    {
        await EnsureFreshTokenAsync(ct);
        var http = BuildClient();

        var resp = await http.GetAsync(
            $"{GraphBase}/me/drive/special/approot:/{BackupFileName}:/content", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<OneDriveToken?> ExchangeCodeAsync(string code, string verifier, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"]          = code,
            ["client_id"]     = ClientId,
            ["redirect_uri"]  = OAuthHelper.RedirectUri,
            ["grant_type"]    = "authorization_code",
            ["code_verifier"] = verifier,
        });
        var resp = await _sharedHttp.PostAsync($"{Authority}/token", form, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var raw = await resp.Content.ReadFromJsonAsync<TokenRaw>(cancellationToken: ct);
        if (raw?.AccessToken == null) return null;
        return new OneDriveToken(
            AccessToken:  raw.AccessToken,
            RefreshToken: raw.RefreshToken ?? "",
            ExpiresAt:    DateTime.UtcNow.AddSeconds(raw.ExpiresIn - 60),
            Email:        null);
    }

    private async Task EnsureFreshTokenAsync(CancellationToken ct)
    {
        if (_token == null) throw new InvalidOperationException("Not connected to OneDrive.");
        if (_token.ExpiresAt > DateTime.UtcNow) return;

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = _token.RefreshToken,
            ["client_id"]     = ClientId,
            ["grant_type"]    = "refresh_token",
        });
        var resp = await _sharedHttp.PostAsync($"{Authority}/token", form, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _token = null;
            if (File.Exists(_tokenPath)) File.Delete(_tokenPath);
            throw new InvalidOperationException("OneDrive session expired. Please reconnect.");
        }
        var raw = await resp.Content.ReadFromJsonAsync<TokenRaw>(cancellationToken: ct);
        _token = _token with
        {
            AccessToken = raw!.AccessToken,
            ExpiresAt   = DateTime.UtcNow.AddSeconds(raw.ExpiresIn - 60),
        };
        SaveToken(_token);
    }

    private async Task<string?> FetchEmailAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{GraphBase}/me?$select=mail,userPrincipalName");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await _sharedHttp.SendAsync(req, ct);
            var me   = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (me.TryGetProperty("mail", out var m) && m.GetString() is string mail) return mail;
            if (me.TryGetProperty("userPrincipalName", out var u)) return u.GetString();
            return null;
        }
        catch { return null; }
    }

    private HttpClient BuildClient()
    {
        _sharedHttp.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
        return _sharedHttp;
    }

    private static OneDriveToken? LoadToken()
    {
        try
        {
            if (!File.Exists(_tokenPath)) return null;
            var encrypted = File.ReadAllBytes(_tokenPath);
            var plaintext = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<OneDriveToken>(Encoding.UTF8.GetString(plaintext));
        }
        catch { return null; }
    }

    private static void SaveToken(OneDriveToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_tokenPath)!);
        var plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(token));
        var encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_tokenPath, encrypted);
    }

    // ── Models ────────────────────────────────────────────────────────────────

    private sealed record OneDriveToken(
        [property: JsonPropertyName("access_token")]  string   AccessToken,
        [property: JsonPropertyName("refresh_token")] string   RefreshToken,
        [property: JsonPropertyName("expires_at")]    DateTime ExpiresAt,
        [property: JsonPropertyName("email")]         string?  Email);

    private sealed class TokenRaw
    {
        [JsonPropertyName("access_token")]  public string  AccessToken  { get; init; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
        [JsonPropertyName("expires_in")]    public int     ExpiresIn    { get; init; } = 3600;
    }

    private sealed class GraphItem
    {
        [JsonPropertyName("id")]     public string? Id     { get; init; }
        [JsonPropertyName("webUrl")] public string? WebUrl { get; init; }
    }

    private sealed class GraphShareLink
    {
        [JsonPropertyName("link")] public GraphLink? Link { get; init; }
    }

    private sealed class GraphLink
    {
        [JsonPropertyName("webUrl")] public string? WebUrl { get; init; }
    }
}
