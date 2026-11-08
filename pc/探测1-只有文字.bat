@echo off
echo.
echo   UIA probe - WITH TEXT ONLY
echo.
echo   Switch to the QQ chat box NOW (you have 3 seconds).
echo   Make sure the box has TEXT but NO image.
echo.
"%~dp0Miaohua.exe" --uia "%~dp0uia-text.txt"
echo.
echo   Done. Result: uia-text.txt
echo.
pause