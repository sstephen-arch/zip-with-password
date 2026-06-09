namespace Starkive.CloudBackup;

/// <summary>
/// Abstraction over a cloud storage provider used for password vault backup.
/// Implementations: GoogleDriveProvider, OneDriveProvider.
/// </summary>
internal interface ICloudProvider
{
    /// <summary>Human-readable name shown in the UI (e.g. "Google Drive").</summary>
    string ProviderName { get; }

    /// <summary>True if the user has connected and the token is valid.</summary>
    bool IsConnected { get; }

    /// <summary>Display name / email of the connected account, or null.</summary>
    string? ConnectedAccount { get; }

    /// <summary>
    /// Launch the OAuth flow. Opens a browser and listens on localhost for the callback.
    /// Returns true on success.
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken ct = default);

    /// <summary>Revoke the stored token and clear local credentials.</summary>
    Task DisconnectAsync();

    /// <summary>Upload encrypted vault bytes, overwriting the previous backup.</summary>
    Task UploadVaultAsync(byte[] encryptedBytes, CancellationToken ct = default);

    /// <summary>
    /// Download the vault bytes. Returns null if no backup exists yet.
    /// </summary>
    Task<byte[]?> DownloadVaultAsync(CancellationToken ct = default);

    /// <summary>
    /// Upload an SSZ file to the user's cloud drive (visible folder, shareable).
    /// Returns a shareable link URL, or null on failure.
    /// </summary>
    Task<string?> UploadSszAsync(string filePath, CancellationToken ct = default);
}
