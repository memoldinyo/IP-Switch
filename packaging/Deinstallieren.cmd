@echo off
rem Entfernt IP-Switch. Profile bleiben erhalten (zum Loeschen: -RemoveData anhaengen).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1" %*
pause
