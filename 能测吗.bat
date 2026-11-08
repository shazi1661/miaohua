@echo off
title Can this app demo Miaohua?
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0cantest.ps1"
echo.
echo ------------------------------------------------
echo  Press any key to close this window
pause >nul