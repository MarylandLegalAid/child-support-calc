<#
.SYNOPSIS
  UI check helper: launches the app, optionally with a case file, and saves PNG captures of the window
  scrolled to the top and to the bottom, for side-by-side comparison with the intended design.

.EXAMPLE
  pwsh tools/ui-check/capture.ps1 -Exe src/MdChildSupport.App/bin/x64/Debug/net10.0-windows10.0.19041.0/win-x64/MdChildSupport.exe `
       -CaseFile samples/shared-oct2025.mdcase -Out $env:TEMP/shared
#>
param(
    [Parameter(Mandatory)] [string]$Exe,
    [string]$CaseFile,
    [Parameter(Mandatory)] [string]$Out,
    [int]$WaitSeconds = 6
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
}
"@

$args = @()
if ($CaseFile) { $args += '"' + (Resolve-Path $CaseFile).Path + '"' }
$p = if ($args) { Start-Process -FilePath $Exe -ArgumentList $args -PassThru } else { Start-Process -FilePath $Exe -PassThru }
Start-Sleep -Seconds $WaitSeconds
$p.Refresh()
$h = $p.MainWindowHandle
[Win]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 500

function Capture([string]$path) {
    $r = New-Object Win+RECT
    [Win]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) | Out-Null   # DWMWA_EXTENDED_FRAME_BOUNDS
    $w = $r.Right - $r.Left; $hh = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap $w, $hh
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [Win]::PrintWindow($h, $hdc, 2) | Out-Null   # PW_RENDERFULLCONTENT: works when the window is covered
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

Capture "$Out-top.png"

$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
$scrollers = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
foreach ($s in $scrollers) {
    $sp = $s.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    if ($sp.Current.VerticallyScrollable) {
        # The first scroll can move focus into the page, which scrolls back to the focused box; repeat until it sticks.
        for ($i = 0; $i -lt 4; $i++) { $sp.SetScrollPercent(-1, 100); Start-Sleep -Milliseconds 400 }
        "scroll: {0:N1}% of {1:N0}% view" -f $sp.Current.VerticalScrollPercent, $sp.Current.VerticalViewSize | Write-Output
        break
    }
}
Start-Sleep -Milliseconds 700
Capture "$Out-bottom.png"
Write-Output "pid=$($p.Id)"
