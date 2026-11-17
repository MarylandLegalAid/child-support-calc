<#
.SYNOPSIS
  Drives the running app through UI Automation (no mouse/keyboard needed) to exercise the dialogs and the overnights
  caution. Prints what it finds. Usage: pwsh tools/ui-check/interact.ps1 -Exe <path> -CaseFile samples/shared-oct2025.mdcase
#>
param([Parameter(Mandatory)] [string]$Exe, [string]$CaseFile)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$AE = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]

$p = Start-Process -FilePath $Exe -ArgumentList ('"' + (Resolve-Path $CaseFile).Path + '"') -PassThru
Start-Sleep -Seconds 8
$p.Refresh()
$root = $AE::FromHandle($p.MainWindowHandle)

function Find([string]$name, [string]$type) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    $all = $root.FindAll($Scope::Descendants, $c)
    foreach ($e in $all) { if (-not $type -or $e.Current.ControlType.ProgrammaticName -eq "ControlType.$type") { return $e } }
    return $null
}
function FindLike([string]$pattern) {
    $all = $root.FindAll($Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) { if ($e.Current.Name -like $pattern) { return $e } }
    return $null
}
function Invoke($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 900 }
function Value($e) { $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value }
function SetValue($e, $v) { $e.SetFocus(); $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($v); Start-Sleep -Milliseconds 300 }
function Texts { $root.FindAll($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text))) | ForEach-Object { $_.Current.Name } }

"title: " + $p.MainWindowTitle

# 1. Multifamily calculator for Father: 2 additional children.
Invoke (Find 'Calculate father multifamily adjustment' 'Button')
$spin = FindLike 'Additional children living in*'
"dialog spinner: " + $spin.Current.Name
$edit = $spin.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
SetValue $edit '2'
$apply = Find 'Apply' 'Button'
$apply.SetFocus(); Start-Sleep -Milliseconds 400
(Texts | Where-Object { $_ -like 'Schedule amount*' }) | ForEach-Object { "preview: $_" }
Invoke $apply
$mf = Find 'Father multifamily adjustment' 'Spinner'
"father multifamily now: " + (Value ($mf.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))))
(Texts | Where-Object { $_ -match 'pays|owed by either|reimburses' }) | ForEach-Object { "result: $_" }
$p.Refresh(); "title: " + $p.MainWindowTitle

# 2. Overnights entered below 92.
$ov = Find 'Mother overnights per year' 'Spinner'
$ovEdit = $ov.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
SetValue $ovEdit '60'
(Find 'Father actual income' 'Spinner').FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit))).SetFocus(); Start-Sleep -Milliseconds 800
"overnights box now: " + (Value $ovEdit)
(Texts | Where-Object { $_ -like '*overnights were entered*' }) | ForEach-Object { "caution: $_" }

# 3. Comments dialog.
Invoke (Find 'Comments' 'Button')
$box = Find 'Comments' 'Edit'
SetValue $box 'Agreed schedule per the parenting plan.'
Invoke (Find 'OK' 'Button')
"comments dialog closed: " + ($null -eq (Find 'Comments' 'Edit'))

# 4. Close with unsaved changes -> "Save changes?" -> Cancel keeps the window.
$wp = $root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
$wp.Close(); Start-Sleep -Milliseconds 1200
(Texts | Where-Object { $_ -like 'Save changes*' -or $_ -like 'Do you want to save*' }) | ForEach-Object { "dialog: $_" }
Invoke (Find 'Cancel' 'Button')
$p.Refresh(); "still running after Cancel: " + (-not $p.HasExited)
"pid=$($p.Id)"
