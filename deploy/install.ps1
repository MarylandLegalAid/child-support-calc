<#
.SYNOPSIS
  Machine-wide install of the Maryland Child Support Calculator from a publish folder. Intended as the install
  command of an Intune Win32 app (runs as SYSTEM). See docs/deployment.md.

.DESCRIPTION
  Copies the self-contained publish output to %ProgramFiles%\Maryland Child Support Calculator, creates a Start menu
  shortcut for all users, and registers the .mdcase file type so double-clicking a case opens it in the app.
#>
param(
    [string]$Source = $PSScriptRoot,
    [string]$Target = (Join-Path $env:ProgramFiles 'Maryland Child Support Calculator')
)
$ErrorActionPreference = 'Stop'
$exeName = 'MdChildSupport.exe'
if (-not (Test-Path (Join-Path $Source $exeName))) { throw "$exeName not found in $Source" }

# 1. Files (replace any previous version).
if (Test-Path $Target) { Remove-Item $Target -Recurse -Force }
New-Item -ItemType Directory -Force $Target | Out-Null
Get-ChildItem $Source -Exclude 'install.ps1', 'uninstall.ps1' | Copy-Item -Destination $Target -Recurse -Force
$exe = Join-Path $Target $exeName

# 2. Start menu shortcut.
$lnk = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Maryland Child Support Calculator.lnk'
$shell = New-Object -ComObject WScript.Shell
$s = $shell.CreateShortcut($lnk)
$s.TargetPath = $exe
$s.WorkingDirectory = $Target
$s.IconLocation = "$exe,0"
$s.Description = 'Maryland child support guidelines calculator'
$s.Save()

# 3. File association: .mdcase -> MdChildSupport.Case
$classes = 'HKLM:\SOFTWARE\Classes'
New-Item -Force "$classes\.mdcase" | Out-Null
Set-ItemProperty "$classes\.mdcase" '(default)' 'MdChildSupport.Case'
New-Item -Force "$classes\.mdcase\OpenWithProgids" | Out-Null
New-ItemProperty -Force "$classes\.mdcase\OpenWithProgids" 'MdChildSupport.Case' -Value '' -PropertyType String | Out-Null
New-Item -Force "$classes\MdChildSupport.Case\DefaultIcon" | Out-Null
Set-ItemProperty "$classes\MdChildSupport.Case" '(default)' 'Child support case'
Set-ItemProperty "$classes\MdChildSupport.Case\DefaultIcon" '(default)' "`"$exe`",0"
New-Item -Force "$classes\MdChildSupport.Case\shell\open\command" | Out-Null
Set-ItemProperty "$classes\MdChildSupport.Case\shell\open\command" '(default)' "`"$exe`" `"%1`""

# 4. Uninstall entry (Apps & features).
$un = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MdChildSupport'
New-Item -Force $un | Out-Null
$version = (Get-Item $exe).VersionInfo.ProductVersion
Set-ItemProperty $un DisplayName 'Maryland Child Support Calculator'
Set-ItemProperty $un DisplayVersion $version
Set-ItemProperty $un Publisher (Get-Item $exe).VersionInfo.CompanyName   # from the brand folder's brand.props
Set-ItemProperty $un DisplayIcon "`"$exe`",0"
Set-ItemProperty $un InstallLocation $Target
Set-ItemProperty $un UninstallString "powershell.exe -ExecutionPolicy Bypass -File `"$Target\uninstall.ps1`""
Set-ItemProperty $un NoModify 1 -Type DWord
Set-ItemProperty $un NoRepair 1 -Type DWord
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') $Target -Force

Write-Output "Installed $version to $Target"
