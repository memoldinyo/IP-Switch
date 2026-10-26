<#
.SYNOPSIS
    Entfernt eine per install.ps1 installierte IP-Switch-Installation.
.PARAMETER RemoveData
    Löscht zusätzlich Profile, Sicherungen und Protokolle (%LOCALAPPDATA%\IPSwitch).
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\IP-Switch'),
    [switch]$RemoveData
)

$ErrorActionPreference = 'Stop'
$target = Join-Path $InstallDir 'IPSwitch.exe'

Get-Process -Name 'IPSwitch' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target } | Stop-Process -Force
Start-Sleep -Milliseconds 500

Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'IP-Switch.lnk') -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'IPSwitch' -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\IPSwitch' -Recurse -ErrorAction SilentlyContinue

# Geplante Aufgabe "Mit Administratorrechten starten" entfernen (Löschen erfordert eine UAC-Bestätigung).
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$taskName = "IP-Switch Autostart (Administrator) $sid"
schtasks.exe /Query /TN $taskName 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host 'Entferne die Aufgabe für den Start mit Administratorrechten (UAC-Bestätigung) ...'
    try {
        Start-Process schtasks.exe -ArgumentList "/Delete /TN `"$taskName`" /F" -Verb RunAs -Wait -WindowStyle Hidden
    }
    catch {
        Write-Warning "Die Aufgabe `"$taskName`" konnte nicht entfernt werden. Bitte in der Aufgabenplanung manuell löschen."
    }
}

if (Test-Path $InstallDir) {
    Remove-Item $InstallDir -Recurse -Force
}

$data = Join-Path $env:LOCALAPPDATA 'IPSwitch'
if ($RemoveData) {
    Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host 'IP-Switch und alle Benutzerdaten wurden entfernt.'
}
else {
    Write-Host 'IP-Switch wurde entfernt.'
    if (Test-Path $data) {
        Write-Host "Profile und Sicherungen bleiben erhalten: $data"
        Write-Host 'Zum vollständigen Entfernen: uninstall.ps1 -RemoveData'
    }
}
