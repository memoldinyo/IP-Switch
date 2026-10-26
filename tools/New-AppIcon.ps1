<#
.SYNOPSIS
    Erzeugt das Programmsymbol (app.ico) mit mehreren Größen (PNG-komprimierte ICO-Einträge).
.DESCRIPTION
    Nur nötig, wenn das Symbol geändert werden soll. Das Ergebnis liegt bereits im Repository.
    Ausführen mit Windows PowerShell 5.1 (System.Drawing ist dort immer verfügbar):
        powershell -ExecutionPolicy Bypass -File tools\New-AppIcon.ps1
#>
param(
    [string]$OutFile = (Join-Path $PSScriptRoot '..\src\IPSwitch.App\Assets\app.ico')
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
