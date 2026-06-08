@echo off
echo Starting CORS file server on port 7842...
PowerShell -ExecutionPolicy Bypass -File "%~dp0_serve.ps1"
pause
