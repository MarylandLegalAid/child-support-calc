<#
.SYNOPSIS
  Generates a brand folder's AppIcon.ico: a calculator whose display shows the brand's logomark (or a "§" glyph when
  no logomark is given). See branding/README.md.

.DESCRIPTION
  The body runs from -Mid at the top to -Deep at the bottom so it holds up on dark taskbars; the display uses -Light;
  the keypad's "=" key uses -Accent. Sizes below 32 px use a simplified layout (no logomark, 2x2 keys) so they stay
  crisp. Each size is rendered natively and stored as a PNG entry in the .ico.

.EXAMPLE
  pwsh tools/branding/make-app-icon.ps1 -OutPath branding/default/AppIcon.ico
  pwsh tools/branding/make-app-icon.ps1 -OutPath branding/local/AppIcon.ico -LogomarkPath branding/local/source/logos/logomark.png `
       -DeepColor '#123456' -MidColor '#234567' -LightColor '#DDEEFF' -AccentColor '#FFCC00' -PreviewPath icon.png
#>
param(
    [Parameter(Mandatory)] [string]$OutPath,
    [string]$LogomarkPath,
    [string]$DeepColor = '#2B3440',
    [string]$MidColor = '#3A4654',
    [string]$LightColor = '#D5DCE3',
    [string]$AccentColor = '#1F7A8C',
    [string]$PreviewPath
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$OutPath = [System.IO.Path]::GetFullPath($OutPath)
$logomark = if ($LogomarkPath) { [System.Drawing.Image]::FromFile((Resolve-Path $LogomarkPath).Path) } else { $null }

$deep  = [System.Drawing.ColorTranslator]::FromHtml($DeepColor)
$mid   = [System.Drawing.ColorTranslator]::FromHtml($MidColor)
$light = [System.Drawing.ColorTranslator]::FromHtml($LightColor)
$alert = [System.Drawing.ColorTranslator]::FromHtml($AccentColor)
$key   = [System.Drawing.Color]::FromArgb(240, 255, 255, 255)
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function Fill($g, $color, $path) {
    $b = New-Object System.Drawing.SolidBrush $color
    $g.FillPath($b, $path); $b.Dispose(); $path.Dispose()
}

# Body: mid at the top to deep at the bottom, so it holds up on dark taskbars.
function FillBody($g, $path) {
    $r = $path.GetBounds()
    $b = New-Object System.Drawing.Drawing2D.LinearGradientBrush $r, $mid, $deep, ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($b, $path); $b.Dispose(); $path.Dispose()
}

# Draws the icon in a 256x256 design space; the Graphics transform scales it to $size.
function Render([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.CompositingQuality = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    if ($size -ge 32) {
        FillBody $g (RoundRect 36 12 184 232 30)               # body
        Fill $g $light (RoundRect 58 34 140 96 14)             # display
        if ($logomark) {
            $fh = 84; $fw = $fh * $logomark.Width / $logomark.Height
            $g.DrawImage($logomark, [System.Drawing.RectangleF]::new(184 - $fw, 40, $fw, $fh))  # logomark, right-aligned like digits
        }
        else {
            $font = New-Object System.Drawing.Font 'Segoe UI', 64, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
            $fmt = New-Object System.Drawing.StringFormat
            $fmt.Alignment = 'Far'; $fmt.LineAlignment = 'Center'
            $b = New-Object System.Drawing.SolidBrush $deep
            $g.DrawString([string][char]0x00A7, $font, $b, [System.Drawing.RectangleF]::new(58, 34, 128, 96), $fmt)  # "§"
            $b.Dispose(); $font.Dispose(); $fmt.Dispose()
        }
        $kw = 40; $kh = 32; $gap = 10
        for ($r = 0; $r -lt 2; $r++) {
            for ($c = 0; $c -lt 3; $c++) {
                $color = if ($r -eq 1 -and $c -eq 2) { $alert } else { $key }
                Fill $g $color (RoundRect (58 + $c * ($kw + $gap)) (148 + $r * ($kh + $gap)) $kw $kh 8)
            }
        }
    }
    else {
        FillBody $g (RoundRect 24 8 208 240 36)                # body
        Fill $g $light (RoundRect 52 36 152 72 14)             # display
        $kw = 64; $kh = 40; $gap = 24
        for ($r = 0; $r -lt 2; $r++) {
            for ($c = 0; $c -lt 2; $c++) {
                $color = if ($r -eq 1 -and $c -eq 1) { $alert } else { $key }
                Fill $g $color (RoundRect (52 + $c * ($kw + $gap)) (132 + $r * ($kh + $gap)) $kw $kh 10)
            }
        }
    }
    $g.Dispose()
    $bmp
}

# Render every size and write an .ico with PNG-compressed entries.
$images = foreach ($s in $sizes) {
    $bmp = Render $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    [pscustomobject]@{ Size = $s; Bitmap = $bmp; Bytes = $ms.ToArray() }
}

$fs = [System.IO.File]::Create($OutPath)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$images.Count)   # ICONDIR
$offset = 6 + 16 * $images.Count
foreach ($i in $images) {                                                   # ICONDIRENTRY
    $dim = if ($i.Size -ge 256) { 0 } else { $i.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$i.Bytes.Length); $w.Write([uint32]$offset)
    $offset += $i.Bytes.Length
}
foreach ($i in $images) { $w.Write($i.Bytes) }
$w.Dispose(); $fs.Dispose()
"Wrote $OutPath ($($images.Count) sizes: $($sizes -join ', '))"

if ($PreviewPath) {
    # Every size at 1:1 on light and dark strips, plus the 256 px icon at the left.
    [int]$pw = 256 + 40 + ($sizes | Where-Object { $_ -lt 256 } | Measure-Object -Sum).Sum + 20 * $sizes.Count
    $sheet = New-Object System.Drawing.Bitmap $pw, 600
    $g = [System.Drawing.Graphics]::FromImage($sheet)
    $g.Clear([System.Drawing.Color]::White)
    $dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(32, 32, 32))
    $g.FillRectangle($dark, 0, 300, $pw, 300)
    foreach ($row in 0, 300) {
        $g.DrawImage(($images | Where-Object Size -eq 256).Bitmap, 20, $row + 22)
        $x = 296
        foreach ($i in $images | Where-Object { $_.Size -lt 256 }) {
            $g.DrawImageUnscaled($i.Bitmap, $x, $row + 150 - [int]($i.Size / 2))
            $x += $i.Size + 20
        }
    }
    $g.Dispose(); $sheet.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png); $sheet.Dispose()
    "Wrote preview $PreviewPath"
}
foreach ($i in $images) { $i.Bitmap.Dispose() }
if ($logomark) { $logomark.Dispose() }
