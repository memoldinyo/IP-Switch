<#
.SYNOPSIS
    Probiert das Store-Paket auf diesem Rechner aus (Entwicklermodus nötig).
.DESCRIPTION
    Registriert artifacts\msix\layout (von build-msix.ps1) als Test-App und startet sie aus dem Startmenü-Eintrag.
    Die Test-App hat einen eigenen Datenordner im Paket; beim ersten Start übernimmt sie Profile und Einstellungen
    aus %LOCALAPPDATA%\IPSwitch. Eine laufende Desktop-Version von IP-Switch vorher beenden, sonst zeigt nur diese
    ihr Fenster. -Remove entfernt die Test-App wieder.
    Ausführen mit Windows PowerShell 5.1 (Appx-Modul):
        powershell -ExecutionPolicy Bypass -File tools\Test-Msix.ps1 [-Remove]
#>
param([switch]$Remove)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$name = (Get-Content (Join-Path $root 'packaging\msix\identity.json') -Raw | ConvertFrom-Json).IdentityName

foreach ($old in @(Get-AppxPackage -Name $name)) {
    Get-Process IPSwitch -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$($old.InstallLocation)*" } | Stop-Process -Force
    Remove-AppxPackage $old.PackageFullName
}
if ($Remove) { 'Test-App entfernt.'; return }

if (Get-Process IPSwitch -ErrorAction SilentlyContinue) {
    Write-Warning 'IP-Switch läuft bereits. Bitte beenden (Tray → Beenden) und das Skript erneut starten.'
    return
}

$test = Join-Path $root 'artifacts\msix\test'
if (Test-Path $test) { Remove-Item $test -Recurse -Force }
Copy-Item (Join-Path $root 'artifacts\msix\layout') $test -Recurse
Add-AppxPackage -Register (Join-Path $test 'AppxManifest.xml')
$pkg = Get-AppxPackage -Name $name
"Registriert: $($pkg.PackageFullName)"

Start-Process "shell:AppsFolder\$($pkg.PackageFamilyName)!IPSwitch"
Start-Sleep 5
$proc = Get-Process IPSwitch -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$test*" }
"Läuft aus dem Paket: $([bool]$proc) $($proc.Path)"
$data = Join-Path $env:LOCALAPPDATA "Packages\$($pkg.PackageFamilyName)\LocalState"
"Datenordner: $data"
Get-ChildItem $data -ErrorAction SilentlyContinue | Select-Object Name
