[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$valPat = [System.Windows.Automation.ValuePattern]::Pattern
$txtPat = [System.Windows.Automation.TextPattern]::Pattern
$root = $AE::RootElement
$trueCond = [System.Windows.Automation.Condition]::TrueCondition

$procIds = @(Get-Process -Name QQ -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$wins = @()
foreach ($procId in $procIds) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $procId)
    try { $wins += @($root.FindAll($TS::Children, $c)) } catch { }
}
"QQ windows: $($wins.Count)"

foreach ($w in $wins) {
    $all = @()
    try { $all = @($w.FindAll($TS::Descendants, $trueCond)) } catch { }
    "=== window pid=$($w.Current.ProcessId) name=[$($w.Current.Name)] descendants=$($all.Count) ==="

    $n = 0
    foreach ($e in $all) {
        $hasVal = $false
        $hasTxt = $false
        $o1 = $null
        $o2 = $null
        try { $hasVal = $e.TryGetCurrentPattern($valPat, [ref]$o1) } catch { }
        try { $hasTxt = $e.TryGetCurrentPattern($txtPat, [ref]$o2) } catch { }
        if (-not ($hasVal -or $hasTxt)) { continue }

        $n++
        if ($n -gt 30) { "   ... output capped"; break }

        $cls = $e.Current.ClassName
        $nm = $e.Current.Name
        $cty = $e.Current.ControlType.ProgrammaticName
        $ro = ''
        $curVal = ''
        if ($hasVal) {
            try {
                $pat = $e.GetCurrentPattern($valPat)
                $ro = $pat.Current.IsReadOnly
                $curVal = $pat.Current.Value
            } catch { }
        }
        if ($curVal.Length -gt 40) { $curVal = $curVal.Substring(0, 40) + '...' }
        $rect = $e.Current.BoundingRectangle
        "   [$n] ctype=$cty cls=[$cls] name=[$nm] readonly=$ro rect=$rect val=[$curVal]"
    }
    "   value/text-capable elements: $n"
}
