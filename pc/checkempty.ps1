Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class E {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
}
"@

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outFile = Join-Path $dir 'checkempty-out.txt'
$logFile = Join-Path $dir 'miaohua.log'

$log = New-Object System.Text.StringBuilder
function Say($s) { [void]$log.AppendLine($s); Write-Host $s }
function Finish() { [IO.File]::WriteAllText($outFile, $log.ToString(), (New-Object Text.UTF8Encoding($false))) }

$helpers = @(Get-Process -Name Miaohua -ErrorAction SilentlyContinue)
Say ("helper processes  : " + $helpers.Count)
if ($helpers.Count -eq 0) { Say 'ABORT: Miaohua.exe is not running'; Finish; exit 1 }
$logLinesBefore = @(Get-Content $logFile -Encoding UTF8).Count

$form = New-Object System.Windows.Forms.Form
$form.Text = 'MiaohuaEmptyTarget'
$form.Size = New-Object System.Drawing.Size(700, 200)
$form.StartPosition = 'CenterScreen'
$tb = New-Object System.Windows.Forms.TextBox
$tb.Multiline = $true
$tb.Dock = 'Fill'
$form.Controls.Add($tb)

$VK_CTRL = 0x11; $VK_SHIFT = 0x10; $VK_M = 0x4D; $VK_ALT = 0x12
$script:step = 0

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 1000
$timer.Add_Tick({
    $script:step++
    switch ($script:step) {
        1 {
            $tb.Text = ''                      # the box is deliberately EMPTY
            [E]::keybd_event($VK_ALT, 0, 0, [IntPtr]::Zero)
            [E]::keybd_event($VK_ALT, 0, 2, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 250
            $form.Activate()
            [void][E]::SetForegroundWindow($form.Handle)
            Start-Sleep -Milliseconds 500
            Set-Clipboard -Value 'STALE-CLIPBOARD-JUNK'
            Say ("t=1s  box              : [" + $tb.Text + "]  len=" + $tb.Text.Length)
            Say ("t=1s  foreground is us : " + ([E]::GetForegroundWindow() -eq $form.Handle))
            Say  't=1s  clipboard seeded with STALE-CLIPBOARD-JUNK'
        }
        2 {
            if ([E]::GetForegroundWindow() -ne $form.Handle) {
                Say 'ABORT: test window lost the foreground - sent NO keystrokes'
                Finish; $timer.Stop(); $form.Close(); return
            }
            Say 't=2s  sending Ctrl+Shift+M'
            [E]::keybd_event($VK_CTRL, 0, 0, [IntPtr]::Zero)
            [E]::keybd_event($VK_SHIFT, 0, 0, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 70
            [E]::keybd_event($VK_M, 0, 0, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 70
            [E]::keybd_event($VK_M, 0, 2, [IntPtr]::Zero)
            [E]::keybd_event($VK_SHIFT, 0, 2, [IntPtr]::Zero)
            [E]::keybd_event($VK_CTRL, 0, 2, [IntPtr]::Zero)
        }
        5 {
            $after = $tb.Text
            Say ("t=5s  box              : [" + $after + "]  len=" + $after.Length)

            $new = @(Get-Content $logFile -Encoding UTF8 | Select-Object -Skip $logLinesBefore)
            Say ("helper log lines added: " + $new.Count)
            foreach ($l in $new) { Say ("    " + $l) }

            $aborted = @($new | Select-String -SimpleMatch 'nothing was copied').Count -gt 0
            $pasted  = @($new | Select-String -SimpleMatch 'transformed').Count -gt 0

            if ($after.Length -eq 0 -and $aborted -and -not $pasted) {
                Say '==> PASS: empty box stayed empty; stale clipboard was NOT pasted back'
            } elseif ($after.Length -gt 0) {
                Say '==> FAIL: content was pasted into the empty box'
            } elseif (-not $aborted) {
                Say '==> FAIL: helper never reached the copy step'
            } else {
                Say '==> FAIL: unexpected state'
            }
            Finish; $timer.Stop(); $form.Close()
        }
    }
})

$timer.Start()
[void]$form.ShowDialog()
