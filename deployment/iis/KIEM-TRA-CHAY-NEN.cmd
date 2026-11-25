@echo off
setlocal
if "%~1"=="" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Watch-IisBackground.ps1" -ObserveMinutes 1440 -IntervalSeconds 60
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Watch-IisBackground.ps1" %*
)
set "observeExitCode=%ERRORLEVEL%"
echo.
echo Result code: %observeExitCode%
echo Reports are in the Logs folder next to this file.
pause
exit /b %observeExitCode%
