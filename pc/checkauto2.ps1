# Auto-mode trigger test against NOTEPAD.
#
# The previous version targeted a PowerShell WinForms window owned by the test
# itself, and a control run proved that window could not be copy-driven from -
# sending Ctrl+A/Ctrl+C to your own thread while you are inside its event handler
# leaves them queued. Notepad is a separate process with its own message loop, and
# this driver script has no window of its own, so nothing here blocks itself.
#
#   Phase A: focus Notepad, type NOTHING   -> must not fire
#   Phase B: focus Notepad and really type -> must fire AND rewrite the text
#
# ASCII-only on purpose: the test rules and the sample text use English markers.

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class N {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
}
"@

$dir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe    = Join-Path $dir 'Miaohua.exe'
$log    = Join-Path $dir 'miaohua.log'
$rules  = Join-Path $dir 'rules.txt'
$backup = Join-Path $dir 'rules.autotest-backup.txt'
$out    = Join-Path $dir 'checkauto-out.txt'

$logText = New-Object System.Text.StringBuilder
function Say($s) { [void]$logText.AppendLine($s); Write-Host $s }
function Save() { [IO.File]::WriteAllText($out, $logText.ToString(), (New-Object Text.UTF8Encoding($false))) }
function AutoCount() {
    if (-not (Test-Path $log)) { return 0 }
    return @(Get-Content $log -Encoding UTF8 | Select-String -SimpleMatch 'auto:').Count
}
function Tap([int]$vk) {
    [N]::keybd_event([byte]$vk, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 35
    [N]::keybd_event([byte]$vk, 0, 2, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 110
}
function Combo([int]$mod, [int]$vk) {
    [N]::keybd_event([byte]$mod, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 40
    [N]::keybd_event([byte]$vk, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 40
    [N]::keybd_event([byte]$vk, 0, 2, [IntPtr]::Zero)
    [N]::keybd_event([byte]$mod, 0, 2, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 120
}

$CTRL = 0x11; $ALT = 0x12; $A = 0x41; $C = 0x43
$KEY_H = 0x48; $KEY_E = 0x45; $KEY_L = 0x4C; $KEY_O = 0x4F; $KEY_X = 0x58

Get-Process -Name Miaohua, Notepad -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch { } }
Start-Sleep -Milliseconds 900
Copy-Item $rules $backup -Force

@'
automode = true
debounce = 700
whitelist = notepad
template = {suffix}{kaomoji}
suffix = [MIAO]
kaomoji = [KAO]
hello => hi
'@ | Set-Content -LiteralPath $rules -Encoding ASCII

$helper = Start-Process -FilePath $exe -ArgumentList '--tray' -PassThru
Start-Sleep -Seconds 4
Say ("helper pid        : " + $helper.Id)

Start-Process notepad | Out-Null
$np = $null
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 500
    $np = Get-Process -Name Notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($np) { break }
}
if (-not $np) { Say 'ABORT: no notepad window'; Stop-Process -Id $helper.Id -Force; Copy-Item $backup $rules -Force; Remove-Item $backup -Force; Save; exit 1 }
Say ("notepad pid       : " + $np.Id)

[void][N]::ShowWindow($np.MainWindowHandle, 9)
[N]::keybd_event($ALT, 0, 0, [IntPtr]::Zero); [N]::keybd_event($ALT, 0, 2, [IntPtr]::Zero)
Start-Sleep -Milliseconds 250
[void][N]::SetForegroundWindow($np.MainWindowHandle)
Start-Sleep -Milliseconds 700
$fgOk = ([N]::GetForegroundWindow() -eq $np.MainWindowHandle)
Say ("notepad foreground: " + $fgOk)
if (-not $fgOk) { Say 'ABORT: could not focus notepad'; Stop-Process -Id $np.Id -Force; Stop-Process -Id $helper.Id -Force; Copy-Item $backup $rules -Force; Remove-Item $backup -Force; Save; exit 1 }

# ---- clear the box first: Win11 Notepad restores the previous session's unsaved
# text, which would otherwise make every run start from different content
Combo $CTRL $A
Tap 0x2E                     # Delete
Start-Sleep -Milliseconds 300
Set-Clipboard -Value 'CLEAR-CHECK'
Start-Sleep -Milliseconds 150
Combo $CTRL $A
Combo $CTRL $C
Start-Sleep -Milliseconds 400
$afterClear = Get-Clipboard -Raw
if ($afterClear -eq 'CLEAR-CHECK') { Say 'box cleared       : yes (empty)' }
else { Say ("box cleared       : NO, still holds [" + $afterClear + "]") }

# Clearing the box used Ctrl+A / Delete / Ctrl+C - that IS input, so the tool may
# legitimately arm and fire once. Let it settle before the measurement window
# starts, otherwise phase A counts the clear step's own keystrokes as a failure.
Say 'settling 2.5s after the clear step...'
Start-Sleep -Milliseconds 2500

# ---- phase A: sit still -------------------------------------------------
$aStart = AutoCount
Say ("PHASE A start, auto lines = $aStart  (typing nothing, waiting 4s)")
Start-Sleep -Seconds 4
$aEnd = AutoCount
Say ("PHASE A end,   auto lines = $aEnd   fired = " + ($aEnd - $aStart))

# ---- phase B: type ------------------------------------------------------
$bStart = AutoCount
Say "PHASE B start, typing hello + xxxxxxxxxxxx"
foreach ($vk in @($KEY_H, $KEY_E, $KEY_L, $KEY_L, $KEY_O)) { Tap $vk }
foreach ($k in 1..12) { Tap $KEY_X }
Say "typed, waiting 2.5s for the debounce to elapse"
Start-Sleep -Milliseconds 2500
$bEnd = AutoCount
Say ("PHASE B end,   auto lines = $bEnd   fired = " + ($bEnd - $bStart))

# ---- read notepad from this process (it has its own message loop, so this works)
Set-Clipboard -Value 'SELFTEST-SENTINEL'
Start-Sleep -Milliseconds 150
Combo $CTRL $A
Combo $CTRL $C
Start-Sleep -Milliseconds 400
$content = Get-Clipboard -Raw
Say ("notepad content   : [" + $content + "]")

Say ''
Say '--- helper auto lines ---'
if (Test-Path $log) { Get-Content $log -Encoding UTF8 | Select-String -SimpleMatch 'auto:' | Select-Object -Last 4 | ForEach-Object { Say ("    " + $_.Line) } }
Say ''

if ($aEnd -eq $aStart) { Say 'PASS A: focusing notepad without typing did NOT fire' }
else { Say '*** FAIL A: fired with nothing typed ***' }

if ($content -like '*[MIAO]*') { Say 'PASS B: typing fired AND the tool rewrote the text' }
elseif ($bEnd -gt $bStart) { Say '*** FAIL B: it fired but did not rewrite ***' }
else { Say '*** FAIL B: typing did not fire at all ***' }

Stop-Process -Id $np.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $helper.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 700
Copy-Item $backup $rules -Force
Remove-Item $backup -Force
Say 'rules.txt restored'
Save
