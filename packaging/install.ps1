<#
.SYNOPSIS
    Installiert IP-Switch für den aktuellen Benutzer (keine Administratorrechte erforderlich).
.DESCRIPTION
    Kopiert IPSwitch.exe nach %LOCALAPPDATA%\Programs\IP-Switch, legt eine Startmenü-Verknüpfung an,
    trägt die Anwendung unter "Apps & Features" ein und startet sie.
.PARAMETER InstallDir
    Zielverzeichnis (Standard: %LOCALAPPDATA%\Programs\IP-Switch).
.PARAMETER Autostart
    IP-Switch bei der Anmeldung automatisch starten.
.PARAMETER NoStart
    Anwendung nach der Installation nicht starten.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\install.ps1 -Autostart
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\IP-Switch'),
    [switch]$Autostart,
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$exe = Join-Path $source 'IPSwitch.exe'
if (-not (Test-Path $exe)) {
    throw "IPSwitch.exe wurde neben dem Skript nicht gefunden ($source)."
}

$target = Join-Path $InstallDir 'IPSwitch.exe'
$running = Get-Process -Name 'IPSwitch' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target }
if ($running) {
    Write-Host 'IP-Switch läuft bereits und wird für die Aktualisierung beendet ...'
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

Write-Host "Installiere nach $InstallDir ..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item $exe $target -Force
foreach ($item in 'README.md', 'README.de.md', 'LICENSE', 'NOTICE', 'uninstall.ps1', 'Deinstallieren.cmd', 'samples', 'docs') {
    $path = Join-Path $source $item
    if (Test-Path $path) {
        Copy-Item $path $InstallDir -Recurse -Force
    }
}

# Startmenü-Verknüpfung
$programs = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $programs 'IP-Switch.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $target
$shortcut.WorkingDirectory = $InstallDir
$shortcut.IconLocation = "$target,0"
$shortcut.Description = 'IP-Konfigurationen verwalten'
$shortcut.Save()

# Eintrag unter "Apps & Features" (benutzerbezogen)
$version = (Get-Item $target).VersionInfo.ProductVersion -replace '\+.*$', ''
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\IPSwitch'
New-Item -Path $uninstallKey -Force | Out-Null
$uninstallScript = Join-Path $InstallDir 'uninstall.ps1'
$values = @{
    DisplayName     = 'IP-Switch'
    DisplayVersion  = $version
    Publisher       = $(if ((Get-Item $target).VersionInfo.CompanyName) { (Get-Item $target).VersionInfo.CompanyName } else { 'Mehmet Aslandag' })
    DisplayIcon     = "$target,0"
    InstallLocation = $InstallDir
    UninstallString = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`""
}
foreach ($name in $values.Keys) {
    Set-ItemProperty -Path $uninstallKey -Name $name -Value $values[$name]
}
Set-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -Type DWord

if ($Autostart) {
    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'IPSwitch' -Value "`"$target`" --autostart"
    Write-Host 'Autostart aktiviert.'
}

Write-Host "IP-Switch $version wurde installiert."
if (-not $NoStart) {
    Start-Process -FilePath $target
}
