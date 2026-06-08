@echo off
setlocal EnableDelayedExpansion
title Zip with Password — Build MSI

echo.
echo ========================================
echo   Zip with Password — Build MSI
echo ========================================
echo.

:: ── Step 1: Build the app exe if not already present ────────────────────────
if not exist "dist\ZipWithPassword.exe" (
    echo [1/4] dist\ZipWithPassword.exe not found — building app first...
    call Build-Exe.bat
    if errorlevel 1 (
        echo [ERROR] Build-Exe.bat failed. Fix errors above then retry.
        pause & exit /b 1
    )
    echo.
) else (
    echo [1/4] dist\ZipWithPassword.exe found — skipping app build.
)

:: ── Step 2: Ensure .NET SDK is present (required by WiX v4) ─────────────────
echo [2/4] Checking for .NET SDK...
where dotnet >nul 2>&1
if errorlevel 1 (
    echo       Not found. Installing via winget...
    winget install --id Microsoft.DotNet.SDK.8 --silent --accept-source-agreements --accept-package-agreements
    if errorlevel 1 (
        echo [ERROR] Could not install .NET SDK automatically.
        echo         Download it from: https://dotnet.microsoft.com/download
        pause & exit /b 1
    )
    :: Refresh PATH
    call RefreshEnv.cmd >nul 2>&1
    set "PATH=%PATH%;%LOCALAPPDATA%\Microsoft\dotnet;%ProgramFiles%\dotnet"
)
dotnet --version
echo.

:: ── Step 3: Ensure WiX v4 tool and UI extension are installed ───────────────
echo [3/4] Checking for WiX v4 toolset...
dotnet tool list -g | findstr /i "wix" >nul 2>&1
if errorlevel 1 (
    echo       Installing WiX v4...
    dotnet tool install --global wix
    if errorlevel 1 (
        echo [ERROR] Failed to install WiX. Check your .NET SDK installation.
        pause & exit /b 1
    )
)

:: Make sure the dotnet tools bin is on PATH
set "PATH=%PATH%;%USERPROFILE%\.dotnet\tools"

echo       Adding WixToolset.UI.wixext extension...
wix extension add --global WixToolset.UI.wixext 2>nul
echo       Done.
echo.

:: ── Step 4: Build the MSI ────────────────────────────────────────────────────
echo [4/4] Compiling MSI...
if not exist "dist" mkdir dist

wix build installer.wxs ^
    -ext WixToolset.UI.wixext ^
    -o dist\ZipWithPassword.msi

if errorlevel 1 (
    echo.
    echo [ERROR] WiX build failed. Check the output above for details.
    pause & exit /b 1
)

echo.
echo ========================================
echo   MSI ready!
echo   dist\ZipWithPassword.msi
echo ========================================
echo.
echo Double-click dist\ZipWithPassword.msi to install.
echo It will appear in Settings ^> Apps ^& Features
echo and Control Panel ^> Programs and Features.
echo.
pause
