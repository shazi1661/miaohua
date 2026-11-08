[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$valPat = [System.Windows.Automation.ValuePattern]::Pattern
$txtPat = [System.Windows.Automation.TextPattern]::Pattern
$root = $AE::RootElement
$trueCond = [System.Windows.Automation.Condition]::TrueCondition
$editCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)

$procIds = @(Get-Process -Name QQ -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$wins = @()
foreach ($procId in $procIds) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $procId)
    try { $wins += @($root.FindAll($TS::Children, $c)) } catch { }
}

# ---- 1. clean up the test string I accidentally left in the search box ----
$cleaned = 0
foreach ($w in $wins) {
    $eds = @()
    try { $eds = @($w.FindAll($TS::Descendants, $editCond)) } catch { }
    foreach ($ed in $eds) {
        $pat = $null
        try { [void]$ed.TryGetCurrentPattern($valPat, [ref]$pat) } catch { }
        if (-not $pat) { continue }
        $v = ''
        try { $v = $pat.Current.Value } catch { }
        if ($v -eq 'MIAOHUA-TEST-123') {
            try { $pat.SetValue(''); $cleaned++; "CLEANED: search box reset to empty" } catch { "CLEAN FAILED: $($_.Exception.Message)" }
        }
    }
}
if ($cleaned -eq 0) { "note: test string not found (already clean)" }

# ---- 2. locate the chat input box by position ----
foreach ($w in $wins) {
    $all = @()
    try { $all = @($w.FindAll($TS::Descendants, $trueCond)) } catch { }
    $wr = $w.Current.BoundingRectangle
    "=== window rect=$wr descendants=$($all.Count) ==="

    $n = 0
    foreach ($e in $all) {
        $r = $e.Current.BoundingRectangle
        $hasVal = $false
        $hasTxt = $false
        $o1 = $null
        $o2 = $null
        try { $hasVal = $e.TryGetCurrentPattern($valPat, [ref]$o1) } catch { }
        try { $hasTxt = $e.TryGetCurrentPattern($txtPat, [ref]$o2) } catch { }
        $foc = $e.Current.IsKeyboardFocusable

        $interesting = ($r.Y -gt 900) -or $foc -or $hasVal
        if (-not $interesting) { continue }

        $n++
        if ($n -gt 300) { "   ... capped"; break }

        $v = ''
        if ($hasVal) { try { $v = $e.GetCurrentPattern($valPat).Current.Value } catch { } }
        if ($v.Length -gt 30) { $v = $v.Substring(0, 30) + '...' }
        "   ctype=$($e.Current.ControlType.ProgrammaticName) name=[$($e.Current.Name)] foc=$foc hasVal=$hasVal hasTxt=$hasTxt rect=$r val=[$v]"
    }
    "   matched: $n"
}
