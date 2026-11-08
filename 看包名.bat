@echo off
title MuMu - current foreground package name
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0getpkg.ps1"
echo.
echo ------------------------------------------------
echo  Press any key to close this window
pause >nul