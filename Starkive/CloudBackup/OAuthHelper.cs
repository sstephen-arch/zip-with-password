using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Starkive.CloudBackup;

/// <summary>
/// Shared OAuth2 / PKCE helpers used by all cloud providers.
/// Opens the user's browser and hosts a short-lived local HTTP server
/// to receive the authorization code.
/// </summary>
internal static class OAuthHelper
{
    internal const int CallbackPort = 7839;
    internal const string CallbackPath = "/oauth/callback";
    internal static string RedirectUri => $"http://localhost:{CallbackPort}{CallbackPath}";

    // ── PKCE helpers ─────────────────────────────────────────────────────────

    internal static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    internal static string GenerateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    internal static string GenerateState()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ── Browser + local redirect listener ────────────────────────────────────

    /// <summary>
    /// Opens the given URL in the system browser, then waits for the OAuth callback
    /// on localhost:{CallbackPort}.  Returns the full query string of the callback.
    /// Throws OperationCanceledException if ct fires or timeout (90 s) expires.
    /// </summary>
    internal static async Task<string> WaitForCallbackAsync(
        string authUrl, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(90));

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{CallbackPort}/");
        listener.Start();

        // Open browser
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(authUrl) { UseShellExecute = true }); }
        catch { throw new InvalidOperationException("Could not open browser to complete sign-in."); }

        // Wait for callback
        var ctx = await listener.GetContextAsync().WaitAsync(cts.Token);
        string query = ctx.Request.Url?.Query ?? "";

        // Respond so the browser tab shows a nice message
        const string html = "<html><body style='font-family:sans-serif;padding:40px'>"
            + "<h2>✅ Connected! You can close this tab.</h2></body></html>";
        var buf = Encoding.UTF8.GetBytes(html);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = buf.Length;
        await ctx.Response.OutputStream.WriteAsync(buf, cts.Token);
        ctx.Response.Close();
        listener.Stop();

        return query;
    }

    internal static string? ExtractParam(string query, string param)
    {
        var parsed = HttpUtility.ParseQueryString(query.TrimStart('?'));
        return parsed[param];
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data)
                  .TrimEnd('=')
                  .Replace('+', '-')
                  .Replace('/', '_');
}
