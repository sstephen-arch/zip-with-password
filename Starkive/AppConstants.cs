namespace Starkive;

internal static class AppConstants
{
    internal const string SupabaseUrl     = "https://aospshdryqifluqyxfjx.supabase.co";
    internal const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImFvc3BzaGRyeXFpZmx1cXl4Zmp4Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODA4NDE0ODEsImV4cCI6MjA5NjQxNzQ4MX0.mMwLOyKFsJ497tPsgVCxsLOX4XE5XL6Mae_QyencuUM";
    internal const string AppVersion      = "1.2.0";

    // ── Cloud drive OAuth credentials ─────────────────────────────────────────
    // Register your apps and paste the credentials here:
    //
    // Google Drive:
    //   https://console.cloud.google.com → APIs & Services → Credentials
    //   Create OAuth 2.0 Client ID (Desktop app)
    //   Enable: Google Drive API
    //
    // OneDrive:
    //   https://portal.azure.com → App registrations → New registration
    //   Platform: Mobile and desktop → Redirect URI: http://localhost:7839/oauth/callback
    //   API permissions: Files.ReadWrite.AppFolder, User.Read, offline_access
    //
    internal const string GoogleDriveClientId     = ""; // e.g. "123456.apps.googleusercontent.com"
    internal const string GoogleDriveClientSecret = ""; // from Google Cloud Console (desktop apps need this)
    internal const string OneDriveClientId        = ""; // e.g. "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
