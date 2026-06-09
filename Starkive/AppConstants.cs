namespace Starkive;

internal static partial class AppConstants
{
    internal const string SupabaseUrl     = "https://aospshdryqifluqyxfjx.supabase.co";
    internal const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImFvc3BzaGRyeXFpZmx1cXl4Zmp4Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODA4NDE0ODEsImV4cCI6MjA5NjQxNzQ4MX0.mMwLOyKFsJ497tPsgVCxsLOX4XE5XL6Mae_QyencuUM";
    internal const string AppVersion      = "1.3.7";

    // ── Cloud drive OAuth credentials ─────────────────────────────────────────
    // Credentials are stored in Secrets.cs which is NOT committed to git.
    // Copy Secrets.cs.template → Secrets.cs and fill in your values.
    // For Google Drive: https://console.cloud.google.com → APIs & Services → Credentials
    // For OneDrive: https://portal.azure.com → App registrations
}
