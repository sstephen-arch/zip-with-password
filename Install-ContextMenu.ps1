<#
.SYNOPSIS
    Installs (or uninstalls) the "Zip with Password..." right-click context menu entry.

.DESCRIPTION
    Registers ZipWithPassword.exe as a shell handler for files and folders.
    Run with -Uninstall to remove the entry.

    The script first looks for ZipWithPassword.exe in the 'dist' subfolder
    (where Build.ps1 puts it). You can override with -ExePath.

.EXAMPLE
    # Install (default)
    .\Install-ContextMenu.ps1

.EXAMPLE
    # Install from a custom path
    .\Install-ContextMenu.ps1 -ExePath "C:\Tools\ZipWithPassword.exe"

.EXAMPLE
    # Remove the context menu entry
    .\Install-ContextMenu.ps1 -Uninstall
#>

#Requires -Version 5.1
param(
    [string] $ExePath   = "",
    [switch] $Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve exe path ─────────────────────────────────────────────────────────
if (-not $Uninstall) {
    if (-not $ExePath) {
        $ExePath = Join-Path $PSScriptRoot "dist\ZipWithPassword.exe"
    }

    if (-not (Test-Path $ExePath)) {
        Write-Host "ERROR: Cannot find ZipWithPassword.exe at:" -ForegroundColor Red
        Write-Host "  $ExePath" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Build the project first by running Build.ps1" -ForegroundColor Cyan
        exit 1
    }

    $ExePath = (Resolve-Path $ExePath).Path
}

# ── Registry targets ─────────────────────────────────────────────────────────
$MenuLabel  = "Zip with Password..."
$KeyName    = "ZipWithPassword"

# Try HKLM (all users) first; fall back to HKCU (current user only)
function Get-RootKey {
    try {
        $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey(
            "SOFTWARE\Classes", $true)
        if ($key) { return @{ Root = $key; Scope = "all users (HKLM)" } }
    } catch {}
    return @{
        Root  = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("SOFTWARE\Classes", $true)
        Scope = "current user only (HKCU)"
    }
}

$targets = @(
    "SOFTWARE\Classes\*\shell",
    "SOFTWARE\Classes\Directory\shell"
)

Write-Host ""

if ($Uninstall) {
    Write-Host "Removing 'Zip with Password...' from context menu..." -ForegroundColor Yellow
    $info = Get-RootKey
    foreach ($path in $targets) {
        $shellKey = $info.Root.OpenSubKey($path, $true)
        if ($shellKey) {
            try   { $shellKey.DeleteSubKeyTree($KeyName) } catch {}
            $shellKey.Dispose()
        }
    }
    $info.Root.Dispose()
    Write-Host "Done — context menu entry removed." -ForegroundColor Green
}
else {
    Write-Host "Installing 'Zip with Password...' context menu entry..." -ForegroundColor Yellow
    Write-Host "Executable: $ExePath" -ForegroundColor Gray

    $info = Get-RootKey
    Write-Host "Scope: $($info.Scope)" -ForegroundColor Gray

    foreach ($path in $targets) {
        $shellKey = $info.Root.OpenSubKey($path, $true)
        if (-not $shellKey) {
            $shellKey = $info.Root.CreateSubKey($path)
        }

        # Create / overwrite menu entry
        $menuKey = $shellKey.CreateSubKey($KeyName)
        $menuKey.SetValue("", $MenuLabel)
        $menuKey.SetValue("Icon", "`"$ExePath`"")

        $cmdKey = $menuKey.CreateSubKey("command")
        $cmdKey.SetValue("", "`"$ExePath`" `"%1`"")

        $cmdKey.Dispose()
        $menuKey.Dispose()
        $shellKey.Dispose()
    }

    $info.Root.Dispose()

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Installed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Right-click any file or folder in Explorer" -ForegroundColor Cyan
    Write-Host "and choose 'Zip with Password...'" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To remove later, run:" -ForegroundColor Gray
    Write-Host "  .\Install-ContextMenu.ps1 -Uninstall" -ForegroundColor Gray
    Write-Host ""
}
