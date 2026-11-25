@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-IisBackground.ps1" %*
set "setupExitCode=%ERRORLEVEL%"
echo.
echo Result code: %setupExitCode%  ^(0 = background reads confirmed, 2 = settings applied but reads not confirmed, 1 = failed^)
echo Reports are in the Logs folder next to this file.
pause
exit /b %setupExitCode%
