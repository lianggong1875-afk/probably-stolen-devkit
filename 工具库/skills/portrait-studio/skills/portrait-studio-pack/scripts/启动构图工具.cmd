@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0framing-launch.ps1" %*
set "framing_exit=%errorlevel%"
if not "%framing_exit%"=="0" pause
exit /b %framing_exit%
