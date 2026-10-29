<#
.SYNOPSIS
    Baut, testet und paketiert IP-Switch.
.DESCRIPTION
    Ergebnis in .\artifacts:
      IPSwitch-<Version>-win-x64.zip             eigenständig (kein .NET nötig), mit Installationsskript
      IPSwitch-<Version>-win-x64-net10.zip       klein, benötigt die .NET 10 Desktop Runtime
      IPSwitch-<Version>-x64.msi                 benutzerbezogenes MSI (sofern WiX verfügbar)
      SHA256SUMS.txt                             Prüfsummen
.PARAMETER Stage
    All (Standard) = alles. Publish = Tests und Veröffentlichen nach artifacts\publish. Package = ZIPs, MSI und
    Prüfsummen aus einem vorhandenen artifacts\publish erzeugen (z. B. nachdem die EXE in der CI signiert wurde).
.PARAMETER SkipTests
    Tests überspringen.
.PARAMETER SkipMsi
    Kein MSI erzeugen (z. B. ohne Zugriff auf nuget.org).
.EXAMPLE
    .\build.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [ValidateSet('All', 'Publish', 'Package')]
    [string]$Stage = 'All',
    [switch]$SkipTests,
    [switch]$SkipMsi
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$app = Join-Path $root 'src\IPSwitch.App\IPSwitch.App.csproj'
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version

function Invoke-Step([string]$title, [scriptblock]$action) {
    Write-Host "`n=== $title ===" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "$title fehlgeschlagen (Exit-Code $LASTEXITCODE)." }
}

function New-Package([string]$publishDir, [string]$zipName) {
    foreach ($file in 'README.md', 'README.de.md', 'LICENSE', 'NOTICE') {
        Copy-Item (Join-Path $root $file) $publishDir -Force
    }
    Copy-Item (Join-Path $root 'docs') $publishDir -Recurse -Force
    Remove-Item (Join-Path $publishDir 'docs\screenshots') -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $root 'samples') $publishDir -Recurse -Force
    Copy-Item (Join-Path $root 'packaging\*') $publishDir -Force
    $zip = Join-Path $artifacts $zipName
    Remove-Item $zip -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zip
    Write-Host "Paket: $zip"
}

$selfContained = Join-Path $artifacts "publish\IPSwitch-$version-win-x64"
$frameworkDependent = Join-Path $artifacts "publish\IPSwitch-$version-win-x64-net10"

if ($Stage -eq 'Package') {
    foreach ($dir in $selfContained, $frameworkDependent) {
        if (-not (Test-Path (Join-Path $dir 'IPSwitch.exe'))) { throw "Veröffentlichte Dateien fehlen: $dir" }
    }
    Get-ChildItem $artifacts -File -ErrorAction SilentlyContinue | Remove-Item -Force
}
elseif (Test-Path $artifacts) {
    # Alte Ergebnisse soweit möglich entfernen. Läuft IP-Switch gerade aus einem älteren Ausgabeordner, ist dessen
    # EXE gesperrt – dieser Ordner bleibt dann stehen. Das stört nicht, da jede Version eigene Ordnernamen hat.
    Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
    $selfContained = Join-Path $artifacts "publish\IPSwitch-$version-win-x64"
$frameworkDependent = Join-Path $artifacts "publish\IPSwitch-$version-win-x64-net10"

if ($Stage -eq 'Package') {
    foreach ($dir in $selfContained, $frameworkDependent) {
        if (-not (Test-Path (Join-Path $dir 'IPSwitch.exe'))) { throw "Veröffentlichte Dateien fehlen: $dir" }
    }
    Get-ChildItem $artifacts -File -ErrorAction SilentlyContinue | Remove-Item -Force
}
elseif (Test-Path $artifacts) {
        $locked = Get-ChildItem $artifacts -Recurse -File | Select-Object -ExpandProperty FullName
        Write-Warning ("Einige alte Dateien sind gesperrt und bleiben erhalten (läuft IP-Switch aus dem artifacts-Ordner?):`n  " +
            ($locked -join "`n  "))
    }
}
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null

if (-not $SkipTests -and $Stage -ne 'Package') {
    Invoke-Step 'Tests' { dotnet test (Join-Path $root 'tests\IPSwitch.Core.Tests\IPSwitch.Core.Tests.csproj') -c $Configuration --nologo }
}

if ($Stage -ne 'Package') {
    Invoke-Step 'Veröffentlichen (eigenständig, Single-File)' {
        # Bewusst OHNE EnableCompressionInSingleFile: Komprimierte Bundles werden beim Start in den Arbeitsspeicher
        # entpackt (gemessen ~120 MB statt ~53 MB Working Set). ZIP und MSI komprimieren die EXE ohnehin.
        dotnet publish $app -c $Configuration -r win-x64 --self-contained true -o $selfContained --nologo `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
    }
    Invoke-Step 'Veröffentlichen (framework-abhängig)' {
        dotnet publish $app -c $Configuration -r win-x64 --self-contained false -o $frameworkDependent --nologo -p:PublishSingleFile=true
    }
    # Symboldateien sind in Release eingebettet; separate .pdb-Dateien werden nicht ausgeliefert.
    Get-ChildItem $selfContained, $frameworkDependent -File -Filter *.pdb | Remove-Item -Force
}

if ($Stage -eq 'Publish') {
    Write-Host "`nVeröffentlicht nach $(Join-Path $artifacts 'publish')" -ForegroundColor Green
    return
}

# Kopien verpacken, damit artifacts\publish nur die (ggf. signierten) EXE-Dateien enthält.
$staging = Join-Path $artifacts 'staging'
foreach ($dir in $selfContained, $frameworkDependent) {
    $target = Join-Path $staging (Split-Path $dir -Leaf)
    Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item $dir $target -Recurse
}
New-Package (Join-Path $staging (Split-Path $selfContained -Leaf)) "IPSwitch-$version-win-x64.zip"
New-Package (Join-Path $staging (Split-Path $frameworkDependent -Leaf)) "IPSwitch-$version-win-x64-net10.zip"
Remove-Item $staging -Recurse -Force

if (-not $SkipMsi) {
    try {
        $wixproj = Join-Path $root 'installer\IPSwitch.Installer.wixproj'
        Invoke-Step 'MSI erstellen (WiX Toolset)' {
            # Der erste Download des WiX-SDK schlägt in manchen Netzen sporadisch fehl – ein zweiter Versuch genügt.
            foreach ($attempt in 1, 2) {
                dotnet build $wixproj -c $Configuration --nologo -p:PublishDir=$selfContained -p:ProductVersion=$version -o (Join-Path $artifacts 'msi')
                if ($LASTEXITCODE -eq 0) { break }
                if ($attempt -eq 1) { Write-Host 'Erneuter Versuch ...' }
            }
        }
        Copy-Item (Join-Path $artifacts "msi\IPSwitch-$version-x64.msi") $artifacts
        Remove-Item (Join-Path $artifacts 'msi') -Recurse -Force
    }
    catch {
        Write-Warning "MSI konnte nicht erstellt werden: $($_.Exception.Message). Die ZIP-Pakete sind trotzdem vollständig."
    }
}

Get-ChildItem $artifacts -File | Where-Object Extension -in '.zip', '.msi' | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
} | Set-Content (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ascii

Write-Host "`nFertig. Ergebnisse in $artifacts" -ForegroundColor Green
Get-ChildItem $artifacts -File | Format-Table Name, @{ n = 'Größe (MB)'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize
