@echo off
echo.
echo   UIA probe - WITH AN IMAGE
echo.
echo   Switch to the QQ chat box NOW (you have 3 seconds).
echo   Make sure the box has AN IMAGE (plus some text).
echo.
"%~dp0Miaohua.exe" --uia "%~dp0uia-image.txt"
echo.
echo   Done. Result: uia-image.txt
echo.
pause