@echo off
title Miaohua - run trace
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0gettrc.ps1"
echo.
echo ------------------------------------------------
echo  Press any key to close this window
pause >nul