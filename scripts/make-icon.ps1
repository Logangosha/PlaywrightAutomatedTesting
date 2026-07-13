#requires -Version 5

# CONVERTS PNG TO ICO

Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$pngPath = Join-Path $root 'ui\RunnerUI\Resources\AppIcon\appicon.png'
$icoPath = Join-Path $root 'ui\RunnerUI\Resources\AppIcon\appicon.ico'

if (-not (Test-Path $pngPath)) { Write-Host "Not found: $pngPath" -ForegroundColor Red; exit 1 }

$sizes = 16, 32, 48, 64, 128, 256
$src = [System.Drawing.Image]::FromFile($pngPath)

$pngBlobs = foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, 0, 0, $s, $s)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose(); $bmp.Dispose()
    ,[byte[]]$bytes
}
$src.Dispose()

$fs = New-Object System.IO.FileStream $icoPath, 'Create'
$bw = New-Object System.IO.BinaryWriter $fs

$bw.Write([uint16]0)              
$bw.Write([uint16]1)              
$bw.Write([uint16]$sizes.Count)   

$offset = 6 + (16 * $sizes.Count) 
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $len = $pngBlobs[$i].Length
    $wh = if ($s -ge 256) { 0 } else { $s }   
    $bw.Write([byte]$wh)            
    $bw.Write([byte]$wh)            
    $bw.Write([byte]0)              
    $bw.Write([byte]0)              
    $bw.Write([uint16]1)            
    $bw.Write([uint16]32)           
    $bw.Write([uint32]$len)         
    $bw.Write([uint32]$offset)      
    $offset += $len
}
foreach ($blob in $pngBlobs) { $bw.Write($blob) }

$bw.Flush(); $bw.Dispose(); $fs.Dispose()
Write-Host "Wrote $icoPath" -ForegroundColor Green
