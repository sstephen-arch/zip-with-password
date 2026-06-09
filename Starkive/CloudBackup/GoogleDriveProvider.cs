using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starkive.CloudBackup;

/// <summary>
/// Google Drive backup provider.
/// Stores a single file "StarkiveVault.enc" in the user's Drive root.
///
/// Setup: Register an OAuth app at https://console.cloud.google.com
///   • Application type: Desktop app
///   • Scope: https://www.googleapis.com/auth/drive.appdata
///   • Paste the Client ID below.
/// </summary>
internal sealed class GoogleDriveProvider : ICloudProvider
{
    // ── Developer credentials — fill these in ────────────────────────────────
    private const string ClientId     = AppConstants.GoogleDriveClientId;
    private const string ClientSecret = AppConstants.GoogleDriveClientSecret; // desktop apps are public clients; leave empty if using PKCE only
    // ─────────────────────────────────────────────────────────────────────────

    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string DriveApiBase  = "https://www.googleapis.com";
    private const string Scope         = "https://www.googleapis.com/auth/drive.appdata https://www.googleapis.com/auth/drive.file email profile";
    private const string BackupFileName = "StarkiveVault.enc";

    private static readonly string _tokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Starkive", "cloud", "gdrive_token.json");

    private GoogleToken? _token;

    public string ProviderName     => "Google Drive";
    public bool   IsConnected      => _token != null && !string.IsNullOrEmpty(_token.AccessToken);
    public string? ConnectedAccount => _token?.Email;

    public GoogleDriveProvider() => _token = LoadToken();

    // ── Connect ───────────────────────────────────────────────────────────────

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        string verifier  = OAuthHelper.GenerateCodeVerifier();
        string challenge = OAuthHelper.GenerateCodeChallenge(verifier);
        string state     = OAuthHelper.GenerateState();

        string authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(OAuthHelper.RedirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(Scope)}"
            + "&access_type=offline"
            + "&prompt=consent"
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
        if (_token?.AccessToken != null)
        {
            try
            {
                using var http = new HttpClient();
                await http.PostAsync(
                    $"https://oauth2.googleapis.com/revoke?token={Uri.EscapeDataString(_token.AccessToken)}",
                    null);
            }
            catch { }
        }
        _token = null;
        if (File.Exists(_tokenPath)) File.Delete(_tokenPath);
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    public async Task UploadVaultAsync(byte[] encryptedBytes, CancellationToken ct = default)
    {
        await EnsureFreshTokenAsync(ct);
        using var http = BuildClient();

        // Find existing file ID (if any)
        string? fileId = await FindBackupFileIdAsync(http, ct);

        var metadata = new { name = BackupFileName, parents = new[] { "appDataFolder" } };
        string metaJson = JsonSerializer.Serialize(metadata);

        HttpResponseMessage resp;
        if (fileId == null)
        {
            // Create
            using var form = new MultipartFormDataContent("boundary");
            form.Add(new StringContent(metaJson, Encoding.UTF8, "application/json"), "metadata");
            form.Add(new ByteArrayContent(encryptedBytes) { Headers = { ContentType = MediaTypeHeaderValue.Parse("application/octet-stream") } }, "file");
            resp = await http.PostAsync($"{DriveApiBase}/upload/drive/v3/files?uploadType=multipart&fields=id", form, ct);
        }
        else
        {
            // Update existing
            resp = await http.PatchAsync(
                $"{DriveApiBase}/upload/drive/v3/files/{fileId}?uploadType=media",
                new ByteArrayContent(encryptedBytes) { Headers = { ContentType = MediaTypeHeaderValue.Parse("application/octet-stream") } },
                ct);
        }

        if (!resp.IsSuccessStatusCode)
        {
            string err = await resp.Content.ReadAsStringAsync(ct);
            AppLog.Write($"GoogleDrive upload failed: {(int)resp.StatusCode} — {err}");
            throw new IOException($"Google Drive upload failed: {(int)resp.StatusCode}");
        }
    }

    // ── Upload SSZ file (shareable) ───────────────────────────────────────────

    public async Task<string?> UploadSszAsync(string filePath, CancellationToken ct = default)
    {
        await EnsureFreshTokenAsync(ct);
        using var http = BuildClient();

        string fileName = Path.GetFileName(filePath);
        byte[] fileBytes = await File.ReadAllBytesAsync(filePath, ct);

        // Upload the file to Drive root (drive.file scope)
        var metadata = new { name = fileName, mimeType = "application/octet-stream" };
        string metaJson = JsonSerializer.Serialize(metadata);

        using var form = new MultipartFormDataContent("boundary");
        form.Add(new StringContent(metaJson, Encoding.UTF8, "application/json"), "metadata");
        form.Add(new ByteArrayContent(fileBytes) { Headers = { ContentType = MediaTypeHeaderValue.Parse("application/octet-stream") } }, "file");

        var uploadResp = await http.PostAsync(
            $"{DriveApiBase}/upload/drive/v3/files?uploadType=multipart&fields=id,webViewLink", form, ct);

        if (!uploadResp.IsSuccessStatusCode)
        {
            string err = await uploadResp.Content.ReadAsStringAsync(ct);
            AppLog.Write($"GoogleDrive SSZ upload failed: {(int)uploadResp.StatusCode} — {err}");
            return null;
        }

        var uploaded = await uploadResp.Content.ReadFromJsonAsync<DriveFile>(cancellationToken: ct);
        if (uploaded?.Id == null) return null;

        // Make it accessible to anyone with the link
        var permResp = await http.PostAsJsonAsync(
            $"{DriveApiBase}/drive/v3/permissions/{uploaded.Id}",
            new { type = "anyone", role = "reader" }, ct);

        if (!permResp.IsSuccessStatusCode)
        {
            AppLog.Write($"GoogleDrive share permission failed: {(int)permResp.StatusCode}");
        }

        // Fetch the shareable link
        var fileResp = await http.GetFromJsonAsync<DriveFile>(
            $"{DriveApiBase}/drive/v3/files/{uploaded.Id}?fields=webViewLink", ct);

        return fileResp?.WebViewLink;
    }

    // ── Download ──────────────────────────────────────────────────────────────

    public async Task<byte[]?> DownloadVaultAsync(CancellationToken ct = default)
    {
        await EnsureFreshTokenAsync(ct);
        using var http = BuildClient();

        string? fileId = await FindBackupFileIdAsync(http, ct);
        if (fileId == null) return null;

        var resp = await http.GetAsync(
            $"{DriveApiBase}/drive/v3/files/{fileId}?alt=media", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string?> FindBackupFileIdAsync(HttpClient http, CancellationToken ct)
    {
        var resp = await http.GetFromJsonAsync<DriveFileList>(
            $"{DriveApiBase}/drive/v3/files?spaces=appDataFolder&q={Uri.EscapeDataString($"name='{BackupFileName}'")}&&fields=files(id)", ct);
        return resp?.Files?.FirstOrDefault()?.Id;
    }

    private async Task<GoogleToken?> ExchangeCodeAsync(string code, string verifier, CancellationToken ct)
    {
        using var http = new HttpClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"]          = code,
            ["client_id"]     = ClientId,
            ["client_secret"] = ClientSecret,
            ["redirect_uri"]  = OAuthHelper.RedirectUri,
            ["grant_type"]    = "authorization_code",
            ["code_verifier"] = verifier,
        });
        var resp = await http.PostAsync(TokenEndpoint, form, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var raw = await resp.Content.ReadFromJsonAsync<GoogleTokenRaw>(cancellationToken: ct);
        if (raw?.AccessToken == null) return null;
        return new GoogleToken(
            AccessToken:  raw.AccessToken,
            RefreshToken: raw.RefreshToken ?? "",
            ExpiresAt:    DateTime.UtcNow.AddSeconds(raw.ExpiresIn - 60),
            Email:        null);
    }

    private async Task EnsureFreshTokenAsync(CancellationToken ct)
    {
        if (_token == null) throw new InvalidOperationException("Not connected to Google Drive.");
        if (_token.ExpiresAt > DateTime.UtcNow) return;

        using var http = new HttpClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = _token.RefreshToken,
            ["client_id"]     = ClientId,
            ["client_secret"] = ClientSecret,
            ["grant_type"]    = "refresh_token",
        });
        var resp = await http.PostAsync(TokenEndpoint, form, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _token = null;
            if (File.Exists(_tokenPath)) File.Delete(_tokenPath);
            throw new InvalidOperationException("Google Drive session expired. Please reconnect.");
        }
        var raw = await resp.Content.ReadFromJsonAsync<GoogleTokenRaw>(cancellationToken: ct);
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
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var info = await http.GetFromJsonAsync<JsonElement>(
                "https://www.googleapis.com/oauth2/v2/userinfo", ct);
            return info.TryGetProperty("email", out var e) ? e.GetString() : null;
        }
        catch { return null; }
    }

    private HttpClient BuildClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
        return client;
    }

    private static GoogleToken? LoadToken()
    {
        try
        {
            if (!File.Exists(_tokenPath)) return null;
            return JsonSerializer.Deserialize<GoogleToken>(File.ReadAllText(_tokenPath));
        }
        catch { return null; }
    }

    private static void SaveToken(GoogleToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_tokenPath)!);
        File.WriteAllText(_tokenPath, JsonSerializer.Serialize(token));
    }

    // ── Models ────────────────────────────────────────────────────────────────

    private sealed record GoogleToken(
        [property: JsonPropertyName("access_token")]  string   AccessToken,
        [property: JsonPropertyName("refresh_token")] string   RefreshToken,
        [property: JsonPropertyName("expires_at")]    DateTime ExpiresAt,
        [property: JsonPropertyName("email")]         string?  Email);

    private sealed class GoogleTokenRaw
    {
        [JsonPropertyName("access_token")]  public string  AccessToken  { get; init; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
        [JsonPropertyName("expires_in")]    public int     ExpiresIn    { get; init; } = 3600;
    }

    private sealed class DriveFileList
    {
        [JsonPropertyName("files")] public List<DriveFile>? Files { get; init; }
    }
    private sealed class DriveFile
    {
        [JsonPropertyName("id")]          public string? Id          { get; init; }
        [JsonPropertyName("webViewLink")] public string? WebViewLink { get; init; }
    }
}
