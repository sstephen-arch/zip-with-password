@echo off
setlocal EnableDelayedExpansion
title Zip with Password — Build Installer

echo.
echo ========================================
echo   Zip with Password — Build Installer
echo ========================================
echo.

:: ── Step 1: Make sure the app exe exists ────────────────────────────────────
if not exist "dist\ZipWithPassword.exe" (
    echo [INFO] dist\ZipWithPassword.exe not found — running Build-Exe.bat first...
    echo.
    call Build-Exe.bat
    if errorlevel 1 (
        echo [ERROR] Build-Exe.bat failed. Fix errors above then retry.
        pause
        exit /b 1
    )
)

echo [OK] dist\ZipWithPassword.exe found.
echo.

:: ── Step 2: Locate or install Inno Setup ────────────────────────────────────
set ISCC=
for %%p in (
    "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
    "%ProgramFiles%\Inno Setup 6\ISCC.exe"
    "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
) do (
    if exist %%p (
        set ISCC=%%p
        goto :found_iscc
    )
)

:: Not found — try to install via winget
echo [INFO] Inno Setup not found. Attempting to install via winget...
winget install --id JRSoftware.InnoSetup --silent --accept-source-agreements --accept-package-agreements
if errorlevel 1 (
    echo.
    echo [ERROR] Automatic install failed.
    echo Please install Inno Setup manually from:
    echo   https://jrsoftware.org/isdl.php
    echo Then re-run this script.
    pause
    exit /b 1
)

:: Retry locating ISCC after install
for %%p in (
    "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
    "%ProgramFiles%\Inno Setup 6\ISCC.exe"
    "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
) do (
    if exist %%p (
        set ISCC=%%p
        goto :found_iscc
    )
)

echo [ERROR] ISCC.exe still not found after install. Please install Inno Setup manually.
pause
exit /b 1

:found_iscc
echo [OK] Inno Setup found: %ISCC%
echo.

:: ── Step 3: Compile the installer ───────────────────────────────────────────
echo [BUILD] Compiling installer...
%ISCC% /Q installer.iss
if errorlevel 1 (
    echo.
    echo [ERROR] Inno Setup compilation failed.
    echo Run installer.iss manually in the Inno Setup IDE for details.
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Installer ready!
echo   dist\ZipWithPassword-Setup.exe
echo ========================================
echo.
echo Double-click dist\ZipWithPassword-Setup.exe to install.
echo The app will appear in Settings -> Apps and Control Panel.
echo.
pause
