<#
.SYNOPSIS
  Removes the machine-wide install made by install.ps1 (Intune uninstall command). See docs/deployment.md.
#>
param([string]$Target = (Join-Path $env:ProgramFiles 'Maryland Child Support Calculator'))
$ErrorActionPreference = 'SilentlyContinue'
Get-Process MdChildSupport | Stop-Process -Force
Remove-Item (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Maryland Child Support Calculator.lnk') -Force
Remove-Item 'HKLM:\SOFTWARE\Classes\MdChildSupport.Case' -Recurse -Force
if ((Get-ItemProperty 'HKLM:\SOFTWARE\Classes\.mdcase').'(default)' -eq 'MdChildSupport.Case') {
    Remove-Item 'HKLM:\SOFTWARE\Classes\.mdcase' -Recurse -Force
}
Remove-Item 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MdChildSupport' -Recurse -Force
Remove-Item $Target -Recurse -Force
Write-Output 'Uninstalled'
