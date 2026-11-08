[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]
$VP = [System.Windows.Automation.ValuePattern]::Pattern
$WP = [System.Windows.Automation.WindowPattern]::Pattern
$root = $AE::RootElement

$procIds = @(Get-Process -Name QQ -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$wins = @()
foreach ($procId in $procIds) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $procId)
    try { $wins += @($root.FindAll($TS::Children, $c)) } catch { }
}

$editCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Edit)
$edit = $null
foreach ($w in $wins) {
    try {
        $e = @($w.FindAll($TS::Descendants, $editCond))
        if ($e.Count -gt 0) { $edit = $e[0]; break }
    } catch { }
}
if (-not $edit) { "no Edit element found"; exit 1 }

"--- Edit element context ---"
"class   : [$($edit.Current.ClassName)]"
"name    : [$($edit.Current.Name)]"
"autoid  : [$($edit.Current.AutomationId)]"
"rect    : $($edit.Current.BoundingRectangle)"
"enabled : $($edit.Current.IsEnabled)   focusable=$($edit.Current.IsKeyboardFocusable)"

"--- ancestor chain ---"
$node = $edit
for ($d = 0; $d -lt 8; $d++) {
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $node = $walker.GetParent($node)
    if (-not $node) { break }
    "  ^$($d+1) ctype=$($node.Current.ControlType.ProgrammaticName) cls=[$($node.Current.ClassName)] name=[$($node.Current.Name)]"
}

"--- sibling edit-like elements under same parent ---"
$parent = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($edit)
if ($parent) {
    $kids = @($parent.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition))
    "parent has $($kids.Count) children"
    foreach ($k in $kids) {
        $isVal = $false; $o = $null
        try { $isVal = $k.TryGetCurrentPattern($VP, [ref]$o) } catch { }
        if ($isVal) {
            $v = ''
            try { $v = $k.GetCurrentPattern($VP).Current.Value } catch { }
            "   child ctype=$($k.Current.ControlType.ProgrammaticName) cls=[$($k.Current.ClassName)] value=[$v]"
        }
    }
}

"--- current value ---"
$vp = $edit.GetCurrentPattern($VP)
$original = $vp.Current.Value
"length  : $($original.Length)"
"value   : [$original]"

"--- write test ---"
$test = 'MIAOHUA-TEST-123'
$setOk = $false
try {
    $vp.SetValue($test)
    $setOk = $true
    Start-Sleep -Milliseconds 600
    $after = $edit.GetCurrentPattern($VP).Current.Value
    "after SetValue : [$after]"
    if ($after -eq $test) { "SETVALUE : WORKS" } else { "SETVALUE : value did not stick" }
} catch {
    "SETVALUE : THREW - $($_.Exception.Message)"
}

"--- restore ---"
try {
    $edit.GetCurrentPattern($VP).SetValue($original)
    Start-Sleep -Milliseconds 400
    $back = $edit.GetCurrentPattern($VP).Current.Value
    "after restore  : [$back]"
    if ($back -eq $original) { "RESTORE : OK" } else { "RESTORE : MISMATCH (original was lost)" }
} catch {
    "RESTORE : THREW - $($_.Exception.Message)"
}
