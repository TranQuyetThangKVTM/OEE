@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Oee.ps1"
set "result=%errorlevel%"
pause
exit /b %result%
