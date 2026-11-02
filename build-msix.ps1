<#
.SYNOPSIS
    Baut das Paket für den Microsoft Store: artifacts\IPSwitch-<Version>-x64.msix
.DESCRIPTION
    Voraussetzung: build.ps1 ist gelaufen (artifacts\publish\IPSwitch-<Version>-win-x64\IPSwitch.exe).
    Das Paket bleibt unsigniert – der Store signiert es beim Veröffentlichen selbst.
    Identität und App-Name stehen in packaging\msix\identity.json (aus Partner Center).
#>
[CmdletBinding()]
param(
    [string]$Identity = (Join-Path $PSScriptRoot 'packaging\msix\identity.json')
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
$app = Join-Path $artifacts "publish\IPSwitch-$version-win-x64"
if (-not (Test-Path (Join-Path $app 'IPSwitch.exe'))) { throw "$app\IPSwitch.exe fehlt – zuerst build.ps1 ausführen." }

# makeappx und makepri: aus dem installierten Windows SDK, sonst aus dem NuGet-Paket von Microsoft.
function Find-SdkTool([string]$name) {
    $sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\$name" -ErrorAction SilentlyContinue |
        Sort-Object FullName | Select-Object -Last 1
    if ($sdk) { return $sdk.FullName }
    $toolsVersion = '10.0.28000.2957'
    $tools = Join-Path $root "obj\sdk-buildtools\$toolsVersion"
    if (-not (Test-Path $tools)) {
        New-Item -ItemType Directory -Force $tools | Out-Null
        $nupkg = "$tools.zip"
        Invoke-WebRequest "https://www.nuget.org/api/v2/package/Microsoft.Windows.SDK.BuildTools/$toolsVersion" -OutFile $nupkg
        Expand-Archive $nupkg $tools
        Remove-Item $nupkg
    }
    (Get-ChildItem "$tools\bin\*\x64\$name" | Select-Object -First 1).FullName
}
$makeappx = Find-SdkTool 'makeappx.exe'
$makepri = Find-SdkTool 'makepri.exe'

# Paketinhalt: Programm, Handbücher, Beispielprofile, Lizenzen, Logos und Manifest (Store verlangt x.y.z.0).
$work = Join-Path $artifacts 'msix'
$layout = Join-Path $work 'layout'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $layout 'Assets'), (Join-Path $layout 'docs'), (Join-Path $layout 'samples') | Out-Null
Copy-Item (Join-Path $app 'IPSwitch.exe') $layout
foreach ($file in 'README.md', 'README.de.md', 'LICENSE', 'NOTICE', 'PRIVACY.md') {
    Copy-Item (Join-Path $root $file) $layout
}
Copy-Item (Join-Path $root 'docs\BENUTZERHANDBUCH.md'), (Join-Path $root 'docs\USER_MANUAL.md') (Join-Path $layout 'docs')
Copy-Item (Join-Path $root 'samples\*.json') (Join-Path $layout 'samples')
Copy-Item (Join-Path $root 'packaging\msix\Assets\*.png') (Join-Path $layout 'Assets')

$id = Get-Content $Identity -Raw | ConvertFrom-Json
$manifest = (Get-Content (Join-Path $root 'packaging\msix\AppxManifest.xml') -Raw).
    Replace('$IdentityName$', $id.IdentityName).
    Replace('$Publisher$', [Security.SecurityElement]::Escape($id.Publisher)).
    Replace('$PublisherDisplayName$', [Security.SecurityElement]::Escape($id.PublisherDisplayName)).
    Replace('$DisplayName$', [Security.SecurityElement]::Escape($id.DisplayName)).
    Replace('$Version$', "$version.0")
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

# Ressourcenindex, damit Windows die passende Logo-Größe und Skalierung findet.
$priconfig = Join-Path $work 'priconfig.xml'
& $makepri createconfig /cf $priconfig /dq en-US /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri createconfig fehlgeschlagen.' }
& $makepri new /pr $layout /cf $priconfig /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri new fehlgeschlagen.' }

$msix = Join-Path $artifacts "IPSwitch-$version-x64.msix"
& $makeappx pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx pack fehlgeschlagen.' }
Get-Item $msix | Select-Object FullName, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
