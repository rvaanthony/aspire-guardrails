@echo off
setlocal
pwsh -NoLogo -NoProfile -File "%~dp0verify.ps1" %*
exit /b %ERRORLEVEL%
