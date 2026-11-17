<#
.SYNOPSIS
  Writes the placeholder title-bar logo for branding/default/BrandLogo.png: a white "§" in a white ring on a
  transparent background. The title bar shows the logo 28 px high on the brand's primary colour.

.EXAMPLE
  pwsh tools/branding/make-placeholder-logo.ps1 -OutPath branding/default/BrandLogo.png
#>
param([Parameter(Mandatory)] [string]$OutPath, [int]$Size = 112)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$OutPath = [System.IO.Path]::GetFullPath($OutPath)

$bmp = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'
$g.Clear([System.Drawing.Color]::Transparent)

$stroke = [float]($Size * 0.07)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $stroke
$g.DrawEllipse($pen, $stroke / 2, $stroke / 2, $Size - $stroke, $Size - $stroke)

$font = New-Object System.Drawing.Font 'Segoe UI', ([float]($Size * 0.58)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
$g.DrawString([string][char]0x00A7, $font, [System.Drawing.Brushes]::White, [System.Drawing.RectangleF]::new(0, 0, $Size, $Size), $fmt)

$pen.Dispose(); $font.Dispose(); $fmt.Dispose(); $g.Dispose()
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"Wrote $OutPath"
