# Does the auto trigger run away by arming itself?
#
# The tool injects Ctrl+A / Ctrl+C / End. Those reset the system idle timer exactly
# like real keys, so counting them as "the user is typing" made the trigger re-arm
# itself the moment it finished: fire -> abort -> End -> looks like typing -> fire...
# The symptom is the box flickering select / deselect forever.
#
# This test needs NO clipboard access - a run-away shows up as auto: lines piling up
# in the log while nothing is being typed, which is exactly what we count.
#
# Rules leave the suffix empty, so every transform aborts and takes the collapse path.

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class L {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    public static void ClickAt(IntPtr h, double fx, double fy) {
        RECT r;
        GetWindowRect(h, out r);
        SetCursorPos((int)(r.L + (r.R - r.L) * fx), (int)(r.T + (r.B - r.T) * fy));
        System.Threading.Thread.Sleep(160);
        mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(70);
        mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(260);
    }
}
"@

$dir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe    = Join-Path $dir 'Miaohua.exe'
$log    = Join-Path $dir 'miaohua.log'
$rules  = Join-Path $dir 'rules.txt'
$backup = Join-Path $dir 'rules.looptest-backup.txt'
$out    = Join-Path $dir 'checkloop-out.txt'

$logText = New-Object System.Text.StringBuilder
function Say($s) { [void]$logText.AppendLine($s); Write-Host $s }
function Save() { [IO.File]::WriteAllText($out, $logText.ToString(), (New-Object Text.UTF8Encoding($false))) }
function AutoCount() {
    if (-not (Test-Path $log)) { return 0 }
    return @(Get-Content $log -Encoding UTF8 | Select-String -SimpleMatch 'auto:').Count
}
function Tap([int]$vk) {
    [L]::keybd_event([byte]$vk, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 35
    [L]::keybd_event([byte]$vk, 0, 2, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 100
}
function Cleanup() {
    Stop-Process -Name Notepad -Force -ErrorAction SilentlyContinue
    Stop-Process -Name Miaohua -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 700
    if (Test-Path $backup) { Copy-Item $backup $rules -Force; Remove-Item $backup -Force }
}

Get-Process -Name Miaohua, Notepad -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch { } }
Start-Sleep -Milliseconds 900
Copy-Item $rules $backup -Force

@'
automode = true
debounce = 300
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
if (-not $np) { Say 'ABORT: no notepad'; Cleanup; Save; exit 1 }

[void][L]::ShowWindow($np.MainWindowHandle, 9)
[L]::keybd_event(0x12, 0, 0, [IntPtr]::Zero); [L]::keybd_event(0x12, 0, 2, [IntPtr]::Zero)
Start-Sleep -Milliseconds 250
[void][L]::SetForegroundWindow($np.MainWindowHandle)
Start-Sleep -Milliseconds 800
$fgOk = ([L]::GetForegroundWindow() -eq $np.MainWindowHandle)
Say ("notepad foreground: " + $fgOk)
if (-not $fgOk) { Say 'ABORT: no focus'; Cleanup; Save; exit 1 }
[L]::ClickAt($np.MainWindowHandle, 0.5, 0.5)
Start-Sleep -Milliseconds 2000

# ---- phase 1: type once, then sit still --------------------------------
$t0 = AutoCount
Say ("PHASE 1: typing 5 keys, then doing NOTHING for 9s   (auto = $t0)")
foreach ($vk in @(0x48,0x45,0x4C,0x4C,0x4F)) { Tap $vk }
Start-Sleep -Seconds 9
$t1 = AutoCount
Say ("PHASE 1 end: auto = $t1   fired = " + ($t1 - $t0) + "   (a run-away would keep climbing)")

# ---- phase 2: click around, type nothing -------------------------------
$t2 = AutoCount
Say ("PHASE 2: clicking around the window 6 times, typing nothing   (auto = $t2)")
foreach ($p in @(@(0.3,0.3), @(0.5,0.4), @(0.7,0.5), @(0.4,0.6), @(0.6,0.3), @(0.5,0.7))) {
    [L]::ClickAt($np.MainWindowHandle, $p[0], $p[1])
}
Start-Sleep -Seconds 6
$t3 = AutoCount
Say ("PHASE 2 end: auto = $t3   fired = " + ($t3 - $t2))

# ---- phase 3: sit completely still for another 9s ----------------------
$t4 = AutoCount
Say ("PHASE 3: nothing at all for 9s   (auto = $t4)")
Start-Sleep -Seconds 9
$t5 = AutoCount
Say ("PHASE 3 end: auto = $t5   fired = " + ($t5 - $t4))

Say ''
if (($t1 - $t0) -le 2) { Say 'PASS 1: one typing burst produced at most one fire (no run-away)' }
else { Say ("*** FAIL 1: one typing burst produced " + ($t1 - $t0) + " fires - the trigger is re-arming itself ***") }

if (($t3 - $t2) -eq 0) { Say 'PASS 2: clicking around did not fire' }
else { Say ("*** FAIL 2: clicking fired " + ($t3 - $t2) + " time(s) ***") }

if (($t5 - $t4) -eq 0) { Say 'PASS 3: sitting still did not fire' }
else { Say ("*** FAIL 3: it fired " + ($t5 - $t4) + " time(s) while idle ***") }

if (Test-Path $log) {
    Say ''
    Say '--- last auto lines ---'
    Get-Content $log -Encoding UTF8 | Select-String -SimpleMatch 'auto:' | Select-Object -Last 5 | ForEach-Object { Say ("    " + $_.Line) }
}

Cleanup
Say 'rules.txt restored'
Save
