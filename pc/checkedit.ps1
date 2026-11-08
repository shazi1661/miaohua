# Regression test for two desktop-only problems:
#
#   A) deleting characters used to ARM the auto trigger, so it fired while the user
#      was editing - and its first action is Ctrl+A
#   B) when a transform aborted, that Ctrl+A selection was LEFT IN PLACE, so the
#      user's next Backspace wiped the whole box instead of one character
#
# The rules below leave the suffix empty, so the transform result is always identical
# to its input: every fire takes the abort path, which is the path that used to leave
# the box selected.
#
# The target is Notepad. Win11 Notepad is flaky as a test bed (tabs, session restore),
# so the content is TYPED and then READ BACK before any assertion runs; if the window
# cannot be driven, the test aborts loudly instead of reporting a bogus verdict.

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class E {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    // Win11 Notepad has a tab strip; being the foreground window does not mean the
    // edit area holds the keyboard focus, so click into it for real.
    public static void ClickCenter(IntPtr h) {
        RECT r;
        GetWindowRect(h, out r);
        SetCursorPos((r.L + r.R) / 2, (r.T + r.B) / 2);
        System.Threading.Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);   // left down
        System.Threading.Thread.Sleep(70);
        mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);   // left up
    }
}
"@

$dir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe    = Join-Path $dir 'Miaohua.exe'
$log    = Join-Path $dir 'miaohua.log'
$rules  = Join-Path $dir 'rules.txt'
$backup = Join-Path $dir 'rules.edittest-backup.txt'
$out    = Join-Path $dir 'checkedit-out.txt'

$logText = New-Object System.Text.StringBuilder
function Say($s) { [void]$logText.AppendLine($s); Write-Host $s }
function Save() { [IO.File]::WriteAllText($out, $logText.ToString(), (New-Object Text.UTF8Encoding($false))) }
function AutoCount() {
    if (-not (Test-Path $log)) { return 0 }
    return @(Get-Content $log -Encoding UTF8 | Select-String -SimpleMatch 'auto:').Count
}
function Tap([int]$vk) {
    [E]::keybd_event([byte]$vk, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 30
    [E]::keybd_event([byte]$vk, 0, 2, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 90
}
function Combo([int]$mod, [int]$vk) {
    [E]::keybd_event([byte]$mod, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 40
    [E]::keybd_event([byte]$vk, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 40
    [E]::keybd_event([byte]$vk, 0, 2, [IntPtr]::Zero)
    [E]::keybd_event([byte]$mod, 0, 2, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 130
}
function Collapse() { Tap 0x23 }      # End

# The helper grabs the clipboard as "backup" before it works and RESTORES it when it
# finishes. A read that happens during one of its cycles therefore gets the backup
# put back over whatever Ctrl+C just copied, so pause the tool around every read.
function Hotkey([int[]]$mods, [int]$vk) {
    foreach ($m in $mods) { [E]::keybd_event([byte]$m, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 25 }
    [E]::keybd_event([byte]$vk, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 45
    [E]::keybd_event([byte]$vk, 0, 2, [IntPtr]::Zero)
    for ($i = $mods.Count - 1; $i -ge 0; $i--) {
        [E]::keybd_event([byte]$mods[$i], 0, 2, [IntPtr]::Zero); Start-Sleep -Milliseconds 25
    }
    Start-Sleep -Milliseconds 300
}
function TogglePause() { Hotkey @(0x11, 0x12, 0x10) 0x50 }   # Ctrl+Alt+Shift+P

function ReadBox() {
    TogglePause
    Start-Sleep -Milliseconds 350
    Set-Clipboard -Value 'SENTINEL'
    Start-Sleep -Milliseconds 200
    Combo 0x11 0x41                   # Ctrl+A
    Combo 0x11 0x43                   # Ctrl+C
    Start-Sleep -Milliseconds 350
    $v = Get-Clipboard -Raw
    TogglePause
    Start-Sleep -Milliseconds 300
    return $v
}
function Cleanup() {
    Stop-Process -Name Notepad -Force -ErrorAction SilentlyContinue
    Stop-Process -Name Miaohua -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 700
    if (Test-Path $backup) { Copy-Item $backup $rules -Force; Remove-Item $backup -Force }
}
function Abort($why) { Say ("ABORT: " + $why); Cleanup; Save; exit 1 }

$CTRL = 0x11; $ALT = 0x12; $BACK = 0x08; $DEL = 0x2E; $V = 0x56
@(0x41,0x42,0x43,0x44,0x45,0x46,0x47,0x48,0x49,0x4A) | Out-Null   # a..j

Get-Process -Name Miaohua, Notepad -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch { } }
Start-Sleep -Milliseconds 900
Copy-Item $rules $backup -Force

# empty suffix => the transform never changes anything => every fire aborts
@'
automode = true
debounce = 700
whitelist = notepad
suffix =
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
if (-not $np) { Abort 'no notepad window' }
Say ("notepad pid       : " + $np.Id)

[void][E]::ShowWindow($np.MainWindowHandle, 9)
[E]::keybd_event($ALT, 0, 0, [IntPtr]::Zero); [E]::keybd_event($ALT, 0, 2, [IntPtr]::Zero)
Start-Sleep -Milliseconds 250
[void][E]::SetForegroundWindow($np.MainWindowHandle)
Start-Sleep -Milliseconds 800
if ([E]::GetForegroundWindow() -ne $np.MainWindowHandle) { Abort 'could not focus notepad' }
Say 'notepad foreground: True'

# put the keyboard focus inside the edit area, not the tab strip
[E]::ClickCenter($np.MainWindowHandle)
Start-Sleep -Milliseconds 600
Say 'clicked into the edit area'

# ---- clear, type the content, read it back -------------------------------
Combo $CTRL 0x41        # Ctrl+A
Tap $DEL                # Delete
Start-Sleep -Milliseconds 400
foreach ($vk in @(0x41,0x42,0x43,0x44,0x45,0x46,0x47,0x48,0x49,0x4A)) { Tap $vk }   # abcdefghij
Start-Sleep -Milliseconds 2500          # let the fire caused by typing settle
$content = ReadBox
Say ("content typed     : [" + $content + "]")
if ($content -ne 'abcdefghij') { Abort "target window is not usable (typed abcdefghij, read back [$content])" }
Collapse
Start-Sleep -Milliseconds 1500

# ---- phase A: deleting must NOT arm the trigger --------------------------
$aStart = AutoCount
Say ("PHASE A start, auto lines = $aStart  (Backspace x5, then wait)")
foreach ($i in 1..5) { Tap $BACK }
Start-Sleep -Milliseconds 2500
$aEnd = AutoCount
$afterA = ReadBox
Say ("after A           : [" + $afterA + "]   auto lines = $aEnd   fired = " + ($aEnd - $aStart))
if ($afterA -ne 'abcde') { Abort "phase A lost text: expected abcde, got [$afterA]" }
Collapse
Start-Sleep -Milliseconds 300

# ---- phase B: typing DOES fire (and this fire aborts) --------------------
$bStart = AutoCount
foreach ($i in 1..5) { Tap 0x58 }       # xxxxx
Start-Sleep -Milliseconds 2500
$bEnd = AutoCount
Say ("PHASE B end,   auto lines = $bEnd   fired = " + ($bEnd - $bStart))
if (Test-Path $log) { Get-Content $log -Encoding UTF8 | Select-String -SimpleMatch 'auto:' | Select-Object -Last 2 | ForEach-Object { Say ("    " + $_.Line) } }

# ---- phase C: one Backspace immediately after the aborted fire ----------
# If the abort left the box selected, this wipes everything.
Say 'PHASE C: one Backspace now (must remove exactly one character)'
Tap $BACK
Start-Sleep -Milliseconds 800

# The helper stores the clipboard as a "backup" before each cycle and puts it back
# afterwards, which collides with a read. Killing it first removes the contention
# entirely - the Backspace has already happened, which is all phase C needs.
Stop-Process -Id $helper.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 1000
Set-Clipboard -Value 'SENTINEL'
Start-Sleep -Milliseconds 250
Combo 0x11 0x41
Combo 0x11 0x43
Start-Sleep -Milliseconds 450
$final = Get-Clipboard -Raw
Say ("final content     : [" + $final + "]  len=" + $final.Length)

Say ''
if (($aEnd - $aStart) -eq 0) { Say 'PASS A: deleting did not arm the trigger' }
else { Say '*** FAIL A: deleting still fires ***' }

if (($bEnd - $bStart) -gt 0) { Say 'PASS B: typing still fires' }
else { Say '*** FAIL B: typing no longer fires ***' }

if ($final.Length -eq 9) { Say 'PASS C: the abort collapsed the selection - one Backspace removed one character' }
elseif ($final.Length -eq 0) { Say '*** FAIL C: box was still fully selected - everything got deleted ***' }
else { Say ("*** FAIL C: unexpected length " + $final.Length + " (expected 9) ***") }

Cleanup
Say 'rules.txt restored'
Save
