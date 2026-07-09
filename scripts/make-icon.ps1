#requires -Version 5
<#
  Builds Resources\AppIcon\appicon.ico from appicon.png. Unpackaged Windows apps need
  a real .ico (not a bare PNG) to set the taskbar/titlebar icon at runtime. Embeds
  several square sizes so Windows picks a crisp one at any icon size.
#>
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

# Hand-build the ICO container: ICONDIR + one ICONDIRENTRY per size, each entry's
# image data is a plain PNG blob (the "PNG-in-ICO" format Windows Vista+ supports).
$fs = New-Object System.IO.FileStream $icoPath, 'Create'
$bw = New-Object System.IO.BinaryWriter $fs

$bw.Write([uint16]0)               # reserved
$bw.Write([uint16]1)               # type = icon
$bw.Write([uint16]$sizes.Count)    # image count

$offset = 6 + (16 * $sizes.Count)  # header + one 16-byte directory entry per image
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $len = $pngBlobs[$i].Length
    $wh = if ($s -ge 256) { 0 } else { $s }   # 0 means 256 per the ICO spec
    $bw.Write([byte]$wh)            # width
    $bw.Write([byte]$wh)            # height
    $bw.Write([byte]0)              # color count (0 = PNG/32-bit)
    $bw.Write([byte]0)              # reserved
    $bw.Write([uint16]1)            # planes
    $bw.Write([uint16]32)           # bit count
    $bw.Write([uint32]$len)         # bytes in resource
    $bw.Write([uint32]$offset)      # offset
    $offset += $len
}
foreach ($blob in $pngBlobs) { $bw.Write($blob) }

$bw.Flush(); $bw.Dispose(); $fs.Dispose()
Write-Host "Wrote $icoPath" -ForegroundColor Green
