<#
.SYNOPSIS
    Builds ZipWithPassword as a self-contained, single-file Windows executable.

.DESCRIPTION
    Requires the .NET 8 SDK: https://dotnet.microsoft.com/download
    Run this script from the Zip+Password folder (where Build.ps1 lives).

.EXAMPLE
    .\Build.ps1
#>

#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir  = Join-Path $PSScriptRoot "ZipWithPassword"
$ProjectFile = Join-Path $ProjectDir "ZipWithPassword.csproj"
$OutputDir   = Join-Path $PSScriptRoot "dist"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Zip with Password — Build Script      " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ── Check for .NET SDK ───────────────────────────────────────────────────────
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: dotnet CLI not found." -ForegroundColor Red
    Write-Host "Install the .NET 8 SDK from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

$sdkVersion = dotnet --version
Write-Host "Using .NET SDK $sdkVersion" -ForegroundColor Green

# ── Restore & publish ────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Building project..." -ForegroundColor Yellow

dotnet publish $ProjectFile `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    --output $OutputDir `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build FAILED (exit code $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

$ExePath = Join-Path $OutputDir "ZipWithPassword.exe"

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build succeeded!" -ForegroundColor Green
Write-Host "  $ExePath" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next step — install the context menu entry:" -ForegroundColor Cyan
Write-Host "  Run Install-ContextMenu.ps1  (or double-click it)" -ForegroundColor Cyan
Write-Host ""
