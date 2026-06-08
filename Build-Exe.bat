@echo off
setlocal EnableDelayedExpansion
title Zip with Password — Build

echo.
echo ========================================
echo   Zip with Password — Build Script
echo ========================================
echo.

:: ── Check Python ────────────────────────────────────────────────────────────
where python >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python not found on PATH.
    echo.
    echo Install Python 3.10+ from https://www.python.org/downloads/
    echo Make sure to check "Add Python to PATH" during install.
    pause
    exit /b 1
)

python --version
echo.

:: ── Install dependencies ─────────────────────────────────────────────────────
echo [1/3] Installing dependencies...
python -m pip install --quiet --upgrade pyzipper pyinstaller
if errorlevel 1 (
    echo [ERROR] pip install failed.
    pause
    exit /b 1
)
echo       Done.
echo.

:: ── Build exe ────────────────────────────────────────────────────────────────
echo [2/3] Building ZipWithPassword.exe...
python -m PyInstaller ^
    --onefile ^
    --windowed ^
    --name ZipWithPassword ^
    --distpath dist ^
    --workpath build\pyinstaller ^
    --specpath build ^
    app.py

if errorlevel 1 (
    echo [ERROR] PyInstaller build failed.
    pause
    exit /b 1
)
echo       Done.
echo.

:: ── Clean up PyInstaller spec/work artefacts ─────────────────────────────────
echo [3/3] Cleaning up build artefacts...
rd /s /q build 2>nul
echo       Done.
echo.

echo ========================================
echo   Build succeeded!
echo   dist\ZipWithPassword.exe
echo ========================================
echo.
echo Next: run Install-ContextMenu.ps1 (right-click -> Run with PowerShell)
echo to add "Zip with Password..." to your Explorer context menu.
echo.
pause
