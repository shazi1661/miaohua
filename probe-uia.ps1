[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]
$root = $AE::RootElement

$procIds = @(Get-Process -Name QQ -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
"QQ process ids: $($procIds -join ', ')"
if ($procIds.Count -eq 0) { "QQ is not running; exit"; exit 0 }

$wins = @()
foreach ($procId in $procIds) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $procId)
    try { $wins += @($root.FindAll($TS::Children, $cond)) } catch { }
}
"top-level windows owned by QQ: $($wins.Count)"
foreach ($w in $wins) {
    "   pid=$($w.Current.ProcessId) cls=$($w.Current.ClassName) offscreen=$($w.Current.IsOffscreen) name='$($w.Current.Name)'"
}

# Chromium builds its accessibility tree lazily: the first query often returns 0.
# Query repeatedly and give it time to come up.
$editCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Edit)
$docCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Document)

foreach ($round in 1..4) {
    $total = 0
    foreach ($w in $wins) {
        try { $total += @($w.FindAll($TS::Descendants, $editCond)).Count } catch { }
        try { $total += @($w.FindAll($TS::Descendants, $docCond)).Count } catch { }
    }
    "round $round -> edit+document elements: $total"
    if ($total -gt 0) { break }
    Start-Sleep -Milliseconds 1800
}

foreach ($w in $wins) {
    foreach ($pair in @(@('Edit', $editCond), @('Document', $docCond))) {
        $els = @()
        try { $els = @($w.FindAll($TS::Descendants, $pair[1])) } catch { }
        if ($els.Count -eq 0) { continue }
        "--- $($pair[0]) in pid=$($w.Current.ProcessId) : $($els.Count) ---"
        $i = 0
        foreach ($e in $els) {
            if ($i -ge 5) { "     ... total $($els.Count)"; break }
            $i++
            $hasValue = $false
            $hasText = $false
            $o1 = $null
            $o2 = $null
            try { $hasValue = $e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$o1) } catch { }
            try { $hasText = $e.TryGetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern, [ref]$o2) } catch { }
            $ro = ''
            if ($hasValue) {
                try { $ro = 'readonly=' + $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.IsReadOnly } catch { }
            }
            "     [$i] cls=$($e.Current.ClassName) focusable=$($e.Current.IsKeyboardFocusable) value=$hasValue text=$hasText $ro"
        }
    }
}
