Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class G {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);

    public static IntPtr MainWindowOf(int pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint p;
            GetWindowThreadProcessId(h, out p);
            if ((int)p != pid) return true;
            if (!IsWindowVisible(h)) return true;
            StringBuilder sb = new StringBuilder(300);
            GetWindowTextW(h, sb, 300);
            if (sb.Length == 0) return true;
            found = h;
            return false;
        }, IntPtr.Zero);
        return found;
    }
}
"@

$dir   = 'D:\harness gzq\009\miaohua\pc'
$exe   = Join-Path $dir 'Miaohua.exe'
$log   = Join-Path $dir 'miaohua.log'
$rules = Join-Path $dir 'rules.txt'

Get-Process -Name Miaohua -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch { } }
Start-Sleep -Milliseconds 800
if (Test-Path $log) { Remove-Item $log -Force }

$before = (Get-FileHash $rules -Algorithm SHA256).Hash

$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
$h = [G]::MainWindowOf($p.Id)
"main window handle  : $h"
if ($h -eq [IntPtr]::Zero) { "ABORT: no window"; exit 1 }

# ALT tap unlocks foreground rights, then focus our own window
[G]::keybd_event(0x12, 0, 0, [IntPtr]::Zero)
[G]::keybd_event(0x12, 0, 2, [IntPtr]::Zero)
Start-Sleep -Milliseconds 250
[void][G]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 700

$fgOk = ([G]::GetForegroundWindow() -eq $h)
"foreground is ours  : $fgOk"
if (-not $fgOk) { "ABORT: could not focus our own window - sent no keys"; exit 1 }

"--- sending Ctrl+Shift+M while our own window is focused ---"
[G]::keybd_event(0x11, 0, 0, [IntPtr]::Zero)
[G]::keybd_event(0x10, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 70
[G]::keybd_event(0x4D, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 70
[G]::keybd_event(0x4D, 0, 2, [IntPtr]::Zero)
[G]::keybd_event(0x10, 0, 2, [IntPtr]::Zero)
[G]::keybd_event(0x11, 0, 2, [IntPtr]::Zero)
Start-Sleep -Seconds 2

$after = (Get-FileHash $rules -Algorithm SHA256).Hash
"rules.txt unchanged : $($before -eq $after)"

"--- log ---"
Get-Content $log -Encoding UTF8 | ForEach-Object { "  $_" }

$hit = @(Get-Content $log -Encoding UTF8 | Select-String -SimpleMatch 'own window, skipped').Count
$changed = @(Get-Content $log -Encoding UTF8 | Select-String -SimpleMatch 'transformed').Count

if ($hit -gt 0 -and $changed -eq 0 -and $before -eq $after) {
    "==> PASS: hotkey on our own window was refused, rules.txt untouched"
} else {
    "==> FAIL: guard did not work (skipped=$hit transformed=$changed rulesSame=$($before -eq $after))"
}
