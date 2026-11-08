Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class W {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

    public static string ForegroundInfo() {
        IntPtr h = GetForegroundWindow();
        StringBuilder sb = new StringBuilder(300);
        GetWindowTextW(h, sb, 300);
        uint pid;
        GetWindowThreadProcessId(h, out pid);
        string proc = "?";
        try { proc = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { }
        return "proc=" + proc + " title=[" + sb.ToString() + "]";
    }
}
"@

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$inFile  = Join-Path $dir 'livetest-in.txt'
$outFile = Join-Path $dir 'livetest2-out.txt'
$logFile = Join-Path $dir 'miaohua.log'

$log = New-Object System.Text.StringBuilder
function Say($s) { [void]$log.AppendLine($s); Write-Host $s }
function Finish() { [IO.File]::WriteAllText($outFile, $log.ToString(), (New-Object Text.UTF8Encoding($false))) }

# --- preconditions: the helper MUST be running, otherwise this test proves nothing
$helpers = @(Get-Process -Name Miaohua -ErrorAction SilentlyContinue)
Say ("helper processes  : " + $helpers.Count + "  (pids: " + (($helpers | ForEach-Object { $_.Id }) -join ',') + ")")
if ($helpers.Count -eq 0) {
    Say 'ABORT: Miaohua.exe is not running - start it first, then run this test'
    Finish
    exit 1
}
$logLinesBefore = if (Test-Path $logFile) { @(Get-Content $logFile -Encoding UTF8).Count } else { 0 }
Say ("log lines before  : " + $logLinesBefore)

$text = [IO.File]::ReadAllText($inFile, [Text.Encoding]::UTF8).Trim()

$form = New-Object System.Windows.Forms.Form
$form.Text = 'MiaohuaTestTarget'
$form.Size = New-Object System.Drawing.Size(760, 260)
$form.StartPosition = 'CenterScreen'

$tb = New-Object System.Windows.Forms.TextBox
$tb.Multiline = $true
$tb.Dock = 'Fill'
$tb.Font = New-Object System.Drawing.Font('Consolas', 14)
$form.Controls.Add($tb)

$VK_CTRL = 0x11; $VK_SHIFT = 0x10; $VK_M = 0x4D
$script:step = 0

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 1000
$timer.Add_Tick({
    $script:step++
    switch ($script:step) {
        1 {
            $tb.Text = $text
            # stealing the foreground is unreliable for a background process, so retry
            $script:focusOk = $false
            $tries = 0
            for ($tries = 1; $tries -le 6; $tries++) {
                [W]::keybd_event(0x12, 0, 0, [IntPtr]::Zero)
                [W]::keybd_event(0x12, 0, 2, [IntPtr]::Zero)
                Start-Sleep -Milliseconds 150
                $form.Activate()
                [void][W]::SetForegroundWindow($form.Handle)
                Start-Sleep -Milliseconds 400
                if ([W]::GetForegroundWindow() -eq $form.Handle) { $script:focusOk = $true; break }
            }
            Say ("t=1s  foreground is target : " + $script:focusOk + "  (after $tries try/tries)")
            Say ("t=1s  BEFORE              : [" + $tb.Text + "]")
        }
        2 {
            # never send keys unless we are certain where they land
            if (-not $script:focusOk -or [W]::GetForegroundWindow() -ne $form.Handle) {
                Say 'ABORT: the test window is not in the foreground - sent NO keystrokes.'
                Say ('       foreground right now: ' + [W]::ForegroundInfo())
                Say  '       Close competing windows and re-run.'
                Finish
                $timer.Stop()
                $form.Close()
                return
            }
            Say  't=2s  sending Ctrl+Shift+M'
            [W]::keybd_event($VK_CTRL, 0, 0, [IntPtr]::Zero)
            [W]::keybd_event($VK_SHIFT, 0, 0, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 70
            [W]::keybd_event($VK_M, 0, 0, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 70
            [W]::keybd_event($VK_M, 0, 2, [IntPtr]::Zero)
            [W]::keybd_event($VK_SHIFT, 0, 2, [IntPtr]::Zero)
            [W]::keybd_event($VK_CTRL, 0, 2, [IntPtr]::Zero)
        }
        3 { Say ("t=3s  text now            : [" + $tb.Text + "]") }
        4 { Say ("t=4s  text now            : [" + $tb.Text + "]") }
        5 {
            $after = $tb.Text
            Say ("t=5s  AFTER               : [" + $after + "]")

            $new = @()
            if (Test-Path $logFile) { $new = @(Get-Content $logFile -Encoding UTF8 | Select-Object -Skip $logLinesBefore) }
            Say ("helper log lines added    : " + $new.Count)
            foreach ($l in $new) { Say ("    " + $l) }

            $transformed = @($new | Select-String -SimpleMatch 'transformed').Count -gt 0
            $changed = ($after -ne $text) -and ($after.Length -gt 2)

            if ($transformed -and $changed) { Say 'RESULT : PASS - transform worked end to end' }
            elseif ($transformed -and -not $changed) { Say 'RESULT : FAIL - helper logged a transform but the text did not change' }
            elseif (-not $transformed -and $changed) { Say 'RESULT : FAIL - text changed but helper never logged a transform' }
            else { Say 'RESULT : FAIL - nothing happened' }

            Finish
            $timer.Stop()
            $form.Close()
        }
    }
})

$timer.Start()
[void]$form.ShowDialog()
