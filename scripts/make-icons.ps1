<#
.SYNOPSIS
    Generates the application icon (.ico) and the MSIX package logos (.png).
.DESCRIPTION
    The generated files are committed: run this script again only to change the design.
    Requires Windows PowerShell 5.1 (System.Drawing).
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot

function New-RoundedRectPath([System.Drawing.RectangleF]$r, [float]$radius) {
    $d = $radius * 2
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Blue rounded square with the classic "crop" mark (two interlocking L shapes) in white.
function New-IconBitmap([int]$size, [float]$padding = 0.04) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $m = [float]($size * $padding)
    $s = [float]($size - 2 * $m)
    $rect = New-Object System.Drawing.RectangleF $m, $m, $s, $s
    $bg = New-RoundedRectPath $rect ([float]($s * 0.22))
    $c1 = [System.Drawing.Color]::FromArgb(255, 0x3A, 0x96, 0xDD)
    $c2 = [System.Drawing.Color]::FromArgb(255, 0x00, 0x5F, 0xB8)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, ([float]45)
    $g.FillPath($brush, $bg)

    $w = [float][Math]::Max(1.6, $s * 0.10)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $w
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    function P([float]$x, [float]$y) { New-Object System.Drawing.PointF ($m + $x * $s), ($m + $y * $s) }
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((P 0.34 0.18), (P 0.34 0.66), (P 0.82 0.66)))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((P 0.18 0.34), (P 0.66 0.34), (P 0.66 0.82)))

    $pen.Dispose(); $brush.Dispose(); $bg.Dispose(); $g.Dispose()
    return $bmp
}

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return $ms.ToArray()
}

function Save-Png([int]$size, [string]$path, [float]$padding = 0.04) {
    $bmp = New-IconBitmap $size $padding
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  $path"
}

# ICO file made of PNG-compressed entries (supported since Windows Vista).
function Save-Ico([int[]]$sizes, [string]$path) {
    $images = foreach ($size in $sizes) { $b = New-IconBitmap $size; , (Get-PngBytes $b); $b.Dispose() }
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([UInt16]1); $bw.Write([UInt16]32)
        $bw.Write([UInt32]$images[$i].Length); $bw.Write([UInt32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $bw.Write([byte[]]$img) }
    $bw.Dispose()
    Write-Host "  $path"
}

Write-Host 'Generated icons:'
Save-Ico @(16, 20, 24, 32, 40, 48, 64, 256) (Join-Path $root 'src\CropPdf\Assets\CropPdf.ico')
Save-Png 44 (Join-Path $root 'package\Assets\Square44x44Logo.png')
Save-Png 150 (Join-Path $root 'package\Assets\Square150x150Logo.png') 0.2
Save-Png 50 (Join-Path $root 'package\Assets\StoreLogo.png')
Save-Png 256 (Join-Path $root 'docs\icon-256.png')
