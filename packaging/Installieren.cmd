@echo off
rem Installiert IP-Switch fuer den aktuellen Benutzer (ohne Administratorrechte).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Autostart %*
if errorlevel 1 pause
