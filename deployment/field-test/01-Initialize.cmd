@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Initialize-Database.ps1"
set "result=%errorlevel%"
pause
exit /b %result%
