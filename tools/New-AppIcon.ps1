<#
.SYNOPSIS
    Erzeugt das Programmsymbol (app.ico) mit mehreren Größen (PNG-komprimierte ICO-Einträge) sowie die Logos
    für das Microsoft-Store-Paket (packaging\msix\Assets).
.DESCRIPTION
    Nur nötig, wenn das Symbol geändert werden soll. Das Ergebnis liegt bereits im Repository.
    Ausführen mit Windows PowerShell 5.1 (System.Drawing ist dort immer verfügbar):
        powershell -ExecutionPolicy Bypass -File tools\New-AppIcon.ps1
#>
param(
    [string]$OutFile = (Join-Path $PSScriptRoot '..\src\IPSwitch.App\Assets\app.ico'),
    [string]$StoreAssets = (Join-Path $PSScriptRoot '..\packaging\msix\Assets')
)

Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(0.5, $size * 0.03)
    $rect = New-RoundedRectPath $pad $pad ($size - 2 * $pad) ($size - 2 * $pad) ($size * 0.2)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 0, $size), ([System.Drawing.Color]::FromArgb(255, 30, 136, 229)), ([System.Drawing.Color]::FromArgb(255, 13, 71, 161))
    $g.FillPath($brush, $rect)

    $white = [System.Drawing.Brushes]::White
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = 'Center'
    $format.LineAlignment = 'Center'

    if ($size -ge 32) {
        $font = New-Object System.Drawing.Font 'Segoe UI', ($size * 0.36), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
        $g.DrawString('IP', $font, $white, (New-Object System.Drawing.RectangleF 0, ($size * 0.04), $size, ($size * 0.56)), $format)

        # Zwei gegenläufige Pfeile (Umschalten)
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1.5, $size * 0.055))
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
        $x1 = $size * 0.24; $x2 = $size * 0.76
        $yA = $size * 0.66; $yB = $size * 0.80; $ah = $size * 0.07
        $g.DrawLine($pen, $x1, $yA, $x2, $yA)
        $g.DrawLine($pen, $x2, $yA, $x2 - $ah, $yA - $ah)
        $g.DrawLine($pen, $x2, $yA, $x2 - $ah, $yA + $ah)
        $g.DrawLine($pen, $x2, $yB, $x1, $yB)
        $g.DrawLine($pen, $x1, $yB, $x1 + $ah, $yB - $ah)
        $g.DrawLine($pen, $x1, $yB, $x1 + $ah, $yB + $ah)
    }
    else {
        $font = New-Object System.Drawing.Font 'Segoe UI', ($size * 0.58), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
        $g.DrawString('IP', $font, $white, (New-Object System.Drawing.RectangleF (-$size * 0.02), ($size * 0.02), ($size * 1.04), $size), $format)
    }

    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$dir = Split-Path -Parent $OutFile
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

$fs = [System.IO.File]::Create($OutFile)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$i].Length); $w.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()
Write-Host "Symbol erstellt: $OutFile"

# Store-Logos: Symbol mittig auf durchsichtiger Fläche, jeweils in 100 % und 200 % Skalierung.
function Save-Tile([int]$width, [int]$height, [int]$icon, [string]$file) {
    $tile = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($tile)
    $g.Clear([System.Drawing.Color]::Transparent)
    $bmp = New-IconBitmap $icon
    $g.DrawImageUnscaled($bmp, [int](($width - $icon) / 2), [int](($height - $icon) / 2))
    $g.Dispose(); $bmp.Dispose()
    $tile.Save((Join-Path $StoreAssets $file), [System.Drawing.Imaging.ImageFormat]::Png)
    $tile.Dispose()
}

if (-not (Test-Path $StoreAssets)) { New-Item -ItemType Directory -Path $StoreAssets | Out-Null }
Get-ChildItem $StoreAssets -Filter *.png | Remove-Item
foreach ($scale in 100, 200) {
    $f = $scale / 100
    Save-Tile (50 * $f) (50 * $f) (50 * $f) "StoreLogo.scale-$scale.png"
    Save-Tile (44 * $f) (44 * $f) (44 * $f) "Square44x44Logo.scale-$scale.png"
    Save-Tile (150 * $f) (150 * $f) (100 * $f) "Square150x150Logo.scale-$scale.png"
    Save-Tile (310 * $f) (150 * $f) (100 * $f) "Wide310x150Logo.scale-$scale.png"
}
# Taskleiste und Startmenü: Symbol ohne Kachelhintergrund in festen Größen.
foreach ($size in 16, 24, 32, 48, 256) {
    Save-Tile $size $size $size "Square44x44Logo.targetsize-$($size)_altform-unplated.png"
}
Write-Host "Store-Logos erstellt: $StoreAssets"
